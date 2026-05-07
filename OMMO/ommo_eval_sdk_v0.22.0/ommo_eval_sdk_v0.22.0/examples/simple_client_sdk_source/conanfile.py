from conan import ConanFile
from conan.tools.cmake import cmake_layout, CMake

class OmmoSdkRecipe(ConanFile):
    settings = "os", "compiler", "build_type", "arch"
    generators = "CMakeToolchain", "CMakeDeps"

    def requirements(self):
        self.requires("grpc/1.65.0")
        self.requires("protobuf/5.27.0")
        self.requires("spdlog/1.14.1")

    def layout(self):
        cmake_layout(self)
    
    def build(self):
        cmake = CMake(self)
        cmake.configure()
        cmake.build()

    def build_requirements(self):
        self.tool_requires("cmake/[>=3.25]")
