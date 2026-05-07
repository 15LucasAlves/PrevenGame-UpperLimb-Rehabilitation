cmake_minimum_required(VERSION 3.22)

project(ommo_sdk VERSION 0.19.0 LANGUAGES CXX C)

# C++ standard will be set per target using target_compile_features

set(THREADS_PREFER_PTHREAD_FLAG ON)
find_package(Threads REQUIRED)

set(temp_spdlog_DIR ${spdlog_DIR})
find_package(spdlog)
if(spdlog_FOUND)
    # when spdlog is provided by conan, spdlog_FOUND is true
    set(_SPDLOG_LIB spdlog::spdlog)
elseif(temp_spdlog_DIR)
    # when spdlog is provided by user, spdlog_FOUND is false, and spdlog_DIR is set to spdlog_DIR-NOTFOUND
    set(spdlog_DIR ${temp_spdlog_DIR})
    message(STATUS "Using user defined spdlog at ${spdlog_DIR}")
    add_library(spdlog INTERFACE)
    target_include_directories(spdlog INTERFACE "${spdlog_DIR}")
    set(_SPDLOG_LIB spdlog)
else()
    message(FATAL_ERROR "spdlog is required to build the SDK. Please provide the include directory of spdlog as spdlog_DIR.")
endif()

## Find protobuf
set(protobuf_MODULE_COMPATIBLE TRUE)
find_package(Protobuf CONFIG REQUIRED)
set(_PROTOBUF_LIBPROTOBUF protobuf::libprotobuf)
set(_PROTOBUF_PROTOC $<TARGET_FILE:protobuf::protoc>)

## Find gRPC
find_package(gRPC CONFIG REQUIRED)
set(_GRPC_GRPCPP_UNSECURE gRPC::grpc++_unsecure)
set(_GRPC_CPP_PLUGIN_EXECUTABLE $<TARGET_FILE:gRPC::grpc_cpp_plugin>)

## Generate proto files
get_filename_component(proto_file "${CMAKE_CURRENT_SOURCE_DIR}/protobuf/ommo_service_api.proto" ABSOLUTE)
message(STATUS "PROTO FILE: ${proto_file}")
get_filename_component(proto_path "${proto_file}" PATH)
set(proto_out_path "${CMAKE_CURRENT_BINARY_DIR}/protobuf")
file(MAKE_DIRECTORY ${proto_out_path})

set(proto_srcs "${proto_out_path}/ommo_service_api.pb.cc")
set(proto_hdrs "${proto_out_path}/ommo_service_api.pb.h")
set(grpc_srcs "${proto_out_path}/ommo_service_api.grpc.pb.cc")
set(grpc_hdrs "${proto_out_path}/ommo_service_api.grpc.pb.h")
set(PROTOBUF_FILES
	${proto_srcs}
	${proto_hdrs}
	${grpc_srcs}
    ${grpc_hdrs}
)

## Command to generate proto files
add_custom_command(
      OUTPUT "${proto_srcs}" "${proto_hdrs}" "${grpc_srcs}" "${grpc_hdrs}"
      COMMAND ${_PROTOBUF_PROTOC}
      ARGS --grpc_out "${proto_out_path}"
        --cpp_out "${proto_out_path}"
        -I "${proto_path}"
        --plugin=protoc-gen-grpc="${_GRPC_CPP_PLUGIN_EXECUTABLE}"
        "${proto_file}"
      DEPENDS "${proto_file}")

option(BUILD_SHARED_LIBS "Build using shared libraries" ON)

message(OMMO_SDK_STATIC=${OMMO_SDK_STATIC})
message(BUILD_SHARED_LIBS=${BUILD_SHARED_LIBS})

# ommo_sdk library will build as SHARED or STATIC depending on BUILD_SHARED_LIBS
set(OMMO_SDK_HEADER_FILES
    include/client_context.h
    include/sdk_types.h
    include/sdk_utils.h
    include/wireless_manager.h
)

set(SOURCE_FILES
    src/client_context.cpp
    src/client_context_impl.h
    src/client_manager.cpp
    src/data_manager.cpp
    src/device_data_storage.cpp
    src/basestation_data_storage.cpp
    src/protobuf_converters.cpp
    src/rpcClientCallData.cpp
    src/rpcOpenDataFrameStreamClientCallData.cpp
    src/rpcOpenTrackingDeviceDataStreamClientCallData.cpp
    src/rpc_base_station_data_stream_client_call_data.cpp
    src/rpc_reference_device_state_stream_client_read_reactor.cpp
    src/rpc_tracking_devices_event_stream_client_read_reactor.cpp
    src/rpc_tracking_group_data_stream_client_call_data.cpp
    src/rpc_tracking_groups_event_stream_client_call_data.cpp
    src/rpc_wireless_management_stream_client_bidi_reactor.cpp
    src/sdk_types.cpp
    src/sdk_utils.cpp
    src/spdlog_logger.cpp
    src/std_out_logger.cpp
    src/wireless_manager.cpp
    src/wireless_manager_impl.h
    src/wireless_manager_wrapper.cpp)

if(BUILD_SHARED_LIBS)
  set(INSTALL_SUBDIR "shared")
  add_library(ommo_sdk SHARED
    ${SOURCE_FILES}
    ${PROTOBUF_FILES})
  set(HEADER_FILES
    ${OMMO_SDK_HEADER_FILES})
else()
  set(INSTALL_SUBDIR "static")
  set(HEADER_FILES
    ${OMMO_SDK_HEADER_FILES}
    include/basestation_data_storage.h
    include/client_manager.h
    include/data_manager.h
    include/device_data_storage.h
    include/logger_base.h
    include/protobuf_converters.h
    include/rpcClientCallData.h
    include/rpcOpenDataFrameStreamClientCallData.h
    include/rpcOpenTrackingDeviceDataStreamClientCallData.h
    include/rpc_base_station_data_stream_client_call_data.h
    include/rpc_reference_device_state_stream_client_read_reactor.h
    include/rpc_tracking_devices_event_stream_client_read_reactor.h
    include/rpc_tracking_group_data_stream_client_call_data.h
    include/rpc_tracking_groups_event_stream_client_call_data.h
    include/rpc_wireless_management_stream_client_bidi_reactor.h
    include/rwlock.h
    include/spdlog_logger.h
    include/std_out_logger.h
    include/wireless_manager_wrapper.h
    ${proto_out_path}/ommo_service_api.pb.h
    ${proto_out_path}/ommo_service_api.grpc.pb.h
  )

  add_library(ommo_sdk STATIC
    ${SOURCE_FILES}
    ${PROTOBUF_FILES})
endif()

set_target_properties(ommo_sdk PROPERTIES PUBLIC_HEADER "${HEADER_FILES}")

target_compile_definitions(ommo_sdk
    PUBLIC
    $<$<PLATFORM_ID:Windows>:_UNICODE>
    $<$<PLATFORM_ID:Windows>:UNICODE>
    $<$<PLATFORM_ID:Windows>:NOMINMAX>
    PRIVATE
    # defined to export shared lib symbols
    OMMO_SDK_EXPORTS)

target_include_directories(ommo_sdk
        PUBLIC
        include
        ${proto_out_path}
        ${Protobuf_INCLUDE_DIRS})

target_link_libraries(ommo_sdk
    PUBLIC
    ${_SPDLOG_LIB}
    ${_PROTOBUF_LIBPROTOBUF}
    ${_GRPC_GRPCPP_UNSECURE})

set_target_properties(ommo_sdk
    PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")

# if $<CONFIG> is empty list, install based on CMAKE_BUILD_TYPE since this isn't a multi-config build
# otherwise install based on the configuration type
# Set the install location for the lib and dll files
string(TOLOWER "${CMAKE_CXX_COMPILER_ID}" COMPILER_NAME)
if (MSVC)
    # MSVC stores the version as <MMmm>
    # MM - Major
    # mm - Minor
    math(EXPR MSVC_MAJOR "${MSVC_VERSION} / 100")
    string(CONCAT COMPILER_VERSION "${MSVC_MAJOR}")
else()
    string(CONCAT COMPILER_VERSION "${CMAKE_CXX_COMPILER_VERSION_MAJOR}")
endif()
string(CONCAT COMPILER_ID "${COMPILER_NAME}" "${COMPILER_VERSION}")

get_property(is_multi_config GLOBAL PROPERTY GENERATOR_IS_MULTI_CONFIG)
if (is_multi_config)
    set(INSTALL_DIR "${CMAKE_SOURCE_DIR}/lib/${COMPILER_ID}/$<CONFIG>/${INSTALL_SUBDIR}")
else()
    set(INSTALL_DIR "${CMAKE_SOURCE_DIR}/lib/${COMPILER_ID}/${CMAKE_BUILD_TYPE}/${INSTALL_SUBDIR}")
endif()


install(TARGETS ommo_sdk
    COMPONENT package
    RUNTIME DESTINATION "${INSTALL_DIR}/bin"
    LIBRARY DESTINATION "${INSTALL_DIR}/lib"
    ARCHIVE DESTINATION "${INSTALL_DIR}/lib"
    PUBLIC_HEADER DESTINATION "${INSTALL_DIR}/include/ommo_sdk"
)

install(TARGETS ommo_sdk COMPONENT conan)
