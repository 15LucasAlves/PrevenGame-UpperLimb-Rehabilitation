from conan import ConanFile
from conan.tools.cmake import CMakeToolchain, CMake, cmake_layout, CMakeDeps
from conan.tools.scm import Git

class ommo_sdkRecipe(ConanFile):
    name = "ommo_sdk"
    package_type = "library"

    # Optional metadata
    license = "<Put the package license here>"
    author = "Ting Liao ting.liao@ommo.co"
    url = "<Package recipe repository url here, for issues about the package>"
    description = "This is an sdk for communicating with Ommo services"
    homepage = "https://ommo.co"
    topics = ("ommo", "motion sensor")

    # Binary configuration
    settings = "os", "compiler", "build_type", "arch"
    options = {"shared": [True, False], "fPIC": [True, False]}
    default_options = {"shared": False, "fPIC": True}

    # Sources are located in the same place as this recipe, copy them to the recipe
    exports_sources = "CMakeLists.txt", "src/*", "include/*", "protobuf/*"

    def set_version(self):
        git = Git(self)
        tag = git.run("describe --tags --dirty")
        self.version = tag[1:]

    def config_options(self):
        if self.settings.os == "Windows":
            self.options.rm_safe("fPIC")

    def configure(self):
        if self.options.shared:
            self.options.rm_safe("fPIC")

    def requirements(self):
        self.requires("grpc/1.72.0")
        self.requires("spdlog/1.14.1")
        if self.options.shared:
            self.requires("protobuf/5.27.0")
        else:
            self.requires("protobuf/5.27.0", transitive_headers=True)

    def build_requirements(self):
        self.tool_requires("cmake/[>=3.25]")
        self.tool_requires("protobuf/5.27.0")

    def layout(self):
        cmake_layout(self)
        print(f"self.folders.source = {self.folders.source}")
        print(f"self.folders.build = {self.folders.build}")
        print(f"self.folders.generators = {self.folders.generators}")

        print(f"self.cpp.package.libs = {self.cpp.package.libs}")
        print(f"self.cpp.package.includedirs = {self.cpp.package.includedirs}")
        print(f"self.cpp.package.libdirs = {self.cpp.package.libdirs}")

    def generate(self):
        deps = CMakeDeps(self)
        deps.generate()
        tc = CMakeToolchain(self)
        tc.generate()

    def build(self):
        cmake = CMake(self)        
        shared_option = {'BUILD_SHARED_LIBS': 'ON'} if self.options.shared else {"BUILD_SHARED_LIBS": 'OFF'}
        cmake.configure( variables=shared_option)
        cmake.build()

    def package(self):
        cmake = CMake(self)
        cmake.install(component="conan")

    def package_info(self):
        if not self.options.shared:
            self.cpp_info.defines.append("OMMO_SDK_STATIC")
        self.cpp_info.libs = ["ommo_sdk"]

