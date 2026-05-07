#
# Copyright 2025 Ommo Technologies, Inc.
#
# Unless required by applicable law or agreed to in writing, software
# is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, either express or implied.
#

cmake_minimum_required(VERSION 3.22)

project(simple_client_sdk_source VERSION 0.18.0 LANGUAGES CXX C)

set(PROJECT_DESCRIPTION "Simple Client Example Using SDK Source Code")

# since we are using the SDK source
# use static libs to link into executable
set(BUILD_SHARED_LIBS OFF)

# add the sdk
add_subdirectory(../../sdk sdk_build)

## Create our executable
# use the same main.cpp as simple_client as the client code is identical
add_executable(${PROJECT_NAME}
    ../simple_client/src/main.cpp
)

target_compile_features(${PROJECT_NAME} PUBLIC cxx_std_17)

target_compile_definitions(${PROJECT_NAME}
    PRIVATE
    # OMMO_SDK_STATIC for static import
    OMMO_SDK_STATIC)

# importing ommo_sdk target will automatically import include_directories, compile definitions and dependencies
target_link_libraries(${PROJECT_NAME}
    PRIVATE
    ommo_sdk
)
