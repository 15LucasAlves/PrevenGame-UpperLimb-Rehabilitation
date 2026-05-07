#
# Copyright 2025 Ommo Technologies, Inc.
#
# Unless required by applicable law or agreed to in writing, software
# is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, either express or implied.
#

cmake_minimum_required(VERSION 3.22)

project(base_station_client VERSION 0.19.0 LANGUAGES CXX C)

set(PROJECT_DESCRIPTION "Example of Requesting Base Station Data Using the Shared SDK Library")

## Create our executable
add_executable(${PROJECT_NAME}
    src/main.cpp
)

target_include_directories(${PROJECT_NAME} PRIVATE
    ../../sdk/include
)

target_compile_features(${PROJECT_NAME} PUBLIC cxx_std_17)

# find the shared lib provided in the SDK to import
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

# get the absolute path of the built libraries
get_filename_component(LIB_DIR "../../sdk/lib/${COMPILER_ID}" ABSOLUTE)

target_link_libraries(${PROJECT_NAME}
    PRIVATE
    # Importing DLLs on Windows requires linking to the .lib file for the DLL
    $<IF:$<PLATFORM_ID:Windows>,$<IF:$<CONFIG:>,${LIB_DIR}/${CMAKE_BUILD_TYPE}/lib/ommo_sdk.lib,${LIB_DIR}/$<CONFIG>/lib/ommo_sdk.lib>,>
    $<IF:$<NOT:$<PLATFORM_ID:Windows>>,$<IF:$<CONFIG:>,${LIB_DIR}/${CMAKE_BUILD_TYPE}/lib/libommo_sdk.so,${LIB_DIR}/$<CONFIG>/lib/libommo_sdk.so>,>
    $<IF:$<NOT:$<PLATFORM_ID:Windows>>,$<IF:$<CONFIG:>,pthread,>,>
)

# copy the linked DLL 
if (WIN32)
add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy
        "$<IF:$<CONFIG:>,${LIB_DIR}/${CMAKE_BUILD_TYPE}/bin/ommo_sdk.dll,${LIB_DIR}/$<CONFIG>/bin/ommo_sdk.dll>"
        "$<TARGET_FILE_DIR:${PROJECT_NAME}>/")
endif()
