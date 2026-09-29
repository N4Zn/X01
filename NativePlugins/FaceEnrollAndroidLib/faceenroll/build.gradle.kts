plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.faceattendance.app"
    compileSdk = 34

    defaultConfig {
        // Matches the game app's current floor (Unity ProjectSettings AndroidMinSdkVersion) —
        // nothing ported from FaceAttendance actually needs API 26 specifically; its original
        // :app module used 26 as a standalone-app choice, not a hard requirement found in code.
        minSdk = 25
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }

    // Model files (onnx) are large binary assets — don't let AAPT try to compress/process them.
    androidResources {
        noCompress += listOf("onnx")
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.google.android.material:material:1.12.0")
    implementation("androidx.exifinterface:exifinterface:1.3.7")

    // Pure-Java MP3 decode for the greeting TTS — see GreetingTts.kt for why (broken vendor
    // mediaswcodec on this tablet's ROM).
    implementation("javazoom:jlayer:1.0.1")

    // OpenCV Java classes (FaceDetectorYN/FaceRecognizerSF/Mat/...). Declared here so this
    // module compiles standalone; Unity's mainTemplate.gradle already re-declares the same
    // dependency for the final app build (see comment there: "dependency của
    // face_recognition_unity.aar") — not duplicated at final APK assembly.
    implementation("org.opencv:opencv:4.9.0")

    // com.serenegiant.usb.* (USBMonitor/UVCCamera) — compileOnly against a local copy of the
    // SAME unityplugin-release.aar Unity already bundles (Assets/Plugins/Android/). Must stay
    // compileOnly, not implementation: those classes + their native .so are already supplied
    // by Unity's own inclusion of that aar at final APK assembly — bundling them again here
    // too would collide (Duplicate class / duplicate native lib) once both aars merge into 1 APK.
    compileOnly(files("libs/unityplugin-release.aar"))
}
