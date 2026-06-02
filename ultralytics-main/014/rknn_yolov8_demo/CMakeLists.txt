cmake_minimum_required(VERSION 3.4.1)

project(rknn_yolov8_demo)

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# skip 3rd-party lib dependencies
set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -Wl,--allow-shlib-undefined")

# install target and libraries
set(CMAKE_INSTALL_PREFIX ${CMAKE_SOURCE_DIR}/install/rknn_yolov8_demo_${CMAKE_SYSTEM_NAME})

set(CMAKE_SKIP_INSTALL_RPATH FALSE)
set(CMAKE_BUILD_WITH_INSTALL_RPATH TRUE)
set(CMAKE_INSTALL_RPATH "${CMAKE_INSTALL_PREFIX}/lib")

# rknn api
if(TARGET_SOC STREQUAL "rk356x")
  set(RKNN_API_PATH ${CMAKE_SOURCE_DIR}/../runtime/RK356X/${CMAKE_SYSTEM_NAME}/librknn_api)
  include_directories(${CMAKE_SOURCE_DIR}/../runtime/RK356X/${CMAKE_SYSTEM_NAME}/librknn_api/include/rknn)
elseif(TARGET_SOC STREQUAL "rk3588")
  set(RKNN_API_PATH ${CMAKE_SOURCE_DIR}/../runtime/RK3588/${CMAKE_SYSTEM_NAME}/librknn_api)
  include_directories(${CMAKE_SOURCE_DIR}/../runtime/RK3588/${CMAKE_SYSTEM_NAME}/librknn_api/include/rknn)
else()
  message(FATAL_ERROR "TARGET_SOC is not set, ref value: rk356x or rk3588 or rv110x")
endif()

set(LIB_ARCH "" CACHE STRING "Target library architecture (aarch64/armhf)")

if (CMAKE_SYSTEM_NAME STREQUAL "Android")
  set(RKNN_RT_LIB ${RKNN_API_PATH}/${CMAKE_ANDROID_ARCH_ABI}/librknnrt.so)
else()
  if(NOT LIB_ARCH)
    if (CMAKE_C_COMPILER MATCHES "aarch64")
      set(LIB_ARCH aarch64)
    else()
      set(LIB_ARCH armhf)
    endif()
  endif()
  set(RKNN_RT_LIB ${RKNN_API_PATH}/${LIB_ARCH}/librknnrt.so)
endif()
include_directories(${RKNN_API_PATH}/include)
include_directories(${CMAKE_SOURCE_DIR}/../3rdparty)

find_package(Threads REQUIRED)

# opencv
option(ENABLE_HIGHGUI "Enable OpenCV HighGUI window display" ON)
option(USE_SYSTEM_OPENCV "Use system-provided OpenCV instead of bundled one" OFF)
if(NOT USE_SYSTEM_OPENCV)
  if (CMAKE_SYSTEM_NAME STREQUAL "Android")
      set(OpenCV_DIR ${CMAKE_SOURCE_DIR}/../3rdparty/opencv/OpenCV-android-sdk/sdk/native/jni/abi-${CMAKE_ANDROID_ARCH_ABI})
else()
  if(NOT LIB_ARCH)
    if (CMAKE_C_COMPILER MATCHES "aarch64")
      set(LIB_ARCH aarch64)
    else()
      set(LIB_ARCH armhf)
    endif()
  endif()
  if(LIB_ARCH STREQUAL "armhf")
    set(OpenCV_DIR ${CMAKE_SOURCE_DIR}/../3rdparty/opencv/opencv-linux-armhf/share/OpenCV)
  else()
    set(OpenCV_DIR ${CMAKE_SOURCE_DIR}/../3rdparty/opencv/opencv-linux-aarch64/share/OpenCV)
  endif()
endif()
endif()
if(ENABLE_HIGHGUI)
  find_package(OpenCV REQUIRED COMPONENTS core imgproc imgcodecs highgui)
else()
  find_package(OpenCV REQUIRED COMPONENTS core imgproc imgcodecs)
endif()

#rga
if(TARGET_SOC STREQUAL "rk356x")
  set(RGA_PATH ${CMAKE_SOURCE_DIR}/../3rdparty/rga/RK356X)
elseif(TARGET_SOC STREQUAL "rk3588")
  set(RGA_PATH ${CMAKE_SOURCE_DIR}/../3rdparty/rga/RK3588)
else()
  message(FATAL_ERROR "TARGET_SOC is not set, ref value: rk356x or rk3588")
endif()
if (CMAKE_SYSTEM_NAME STREQUAL "Android")
  set(RGA_LIB ${RGA_PATH}/lib/Android/${CMAKE_ANDROID_ARCH_ABI}/librga.so)
else()
  if(NOT LIB_ARCH)
    if (CMAKE_C_COMPILER MATCHES "aarch64")
      set(LIB_ARCH aarch64)
    else()
      set(LIB_ARCH armhf)
    endif()
  endif()
  set(RGA_LIB ${RGA_PATH}/lib/Linux//${LIB_ARCH}/librga.so)
endif()
include_directories( ${RGA_PATH}/include)

set(CMAKE_INSTALL_RPATH "lib")

# rknn_yolov8_demo
include_directories( ${CMAKE_SOURCE_DIR}/include)

add_executable(rknn_yolov8_demo
        src/main.cc
        src/postprocess.cc
)

if(ENABLE_HIGHGUI)
  target_compile_definitions(rknn_yolov8_demo PRIVATE USE_OPENCV_HIGHGUI)
endif()

target_link_libraries(rknn_yolov8_demo
  ${RKNN_RT_LIB}
  ${RGA_LIB}
  ${OpenCV_LIBS}
  Threads::Threads
)


# install target and libraries
set(CMAKE_INSTALL_PREFIX ${CMAKE_SOURCE_DIR}/install/rknn_yolov8_demo_${CMAKE_SYSTEM_NAME})
install(TARGETS rknn_yolov8_demo DESTINATION ./)

install(PROGRAMS ${RKNN_RT_LIB} DESTINATION lib)
install(PROGRAMS ${RGA_LIB} DESTINATION lib)
install(DIRECTORY model DESTINATION ./)
