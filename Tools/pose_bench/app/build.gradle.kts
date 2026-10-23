plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.eduxplore.posebench"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.eduxplore.posebench"
        minSdk = 24
        targetSdk = 29
        versionCode = 1
        versionName = "1.0"
        ndk { abiFilters += "arm64-v8a" }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    androidResources { noCompress += listOf("task", "tflite") }
}

dependencies {
    implementation("com.google.mediapipe:tasks-vision:0.10.14")
}
