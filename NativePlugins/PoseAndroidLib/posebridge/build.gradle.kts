plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.eduxplore.posebridge"
    compileSdk = 34

    defaultConfig {
        minSdk = 24
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }

    androidResources { noCompress += listOf("task") }
}

dependencies {
    // Unity không tự kéo Maven dependency của aar phẳng: phải khai lại trong Assets/Plugins/Android/mainTemplate.gradle.
    implementation("com.google.mediapipe:tasks-vision:0.10.14")
}
