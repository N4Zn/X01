plugins {
    id("com.android.library")
}

android {
    namespace = "com.eduxplore.lidar"
    compileSdk = 34

    defaultConfig {
        minSdk = 22
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
}
