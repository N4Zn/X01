package com.faceattendance.app

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import android.net.Uri
import android.os.Handler
import android.os.Looper
import android.util.Log
import javazoom.jl.decoder.Bitstream
import javazoom.jl.decoder.Decoder
import javazoom.jl.decoder.SampleBuffer
import org.json.JSONObject
import java.io.File
import java.io.FileInputStream
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import java.security.MessageDigest
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Locale
import java.util.concurrent.Executors

/**
 * Attendance greeting: builds a varied Vietnamese greeting sentence (some variants weather-
 * aware) for whoever just checked in, reads it aloud via Google Translate's unofficial
 * translate_tts endpoint (same trick as 0.Thư viện nội dung/Tool tạo asset/game_tts_v2.0.html's
 * "Google TTS" engine - native Android isn't subject to CORS, so no proxy needed), and also
 * owns the Hanoi weather data shown in MainActivity's always-on info panel. This is NOT the
 * official (paid, API-key-gated) Google Cloud TTS API - it's an undocumented endpoint that
 * could change or rate-limit without notice, so failures are swallowed (text still shows, just
 * no audio) rather than crashing.
 */
class GreetingTts(context: Context) {
    private val executor = Executors.newSingleThreadExecutor()
    // Separate from `executor`: startWeatherRefresh() below submits a perpetual while(true)
    // loop (sleeping 30 minutes between iterations) that never returns. Sharing a single-
    // thread executor between that loop and speak()'s on-demand fetch/play tasks meant every
    // speak() call queued behind the loop forever and never actually ran - TTS never played.
    private val weatherExecutor = Executors.newSingleThreadExecutor()
    private val mainHandler = Handler(Looper.getMainLooper())
    private val cacheDir = File(context.cacheDir, "tts").apply { mkdirs() }
    private var audioTrack: AudioTrack? = null
    private val lastTemplate = mutableMapOf("single" to "", "pair" to "")

    // Hardcoded to Hanoi - IP-based geolocation was tried and dropped: Vietnamese ISPs often
    // route/NAT traffic through a hub city, so free IP-geolocation databases regularly report
    // the wrong city (tested live: reported Ho Chi Minh City while running elsewhere).
    private val locationName = "Hà Nội, Việt Nam"
    private val lat = 21.0285
    private val lon = 105.8542
    private val timezone = "Asia/Bangkok"

    @Volatile private var weather: JSONObject? = null
    @Volatile private var dailyMax: Double? = null
    @Volatile private var dailyMin: Double? = null
    @Volatile private var dailyCode: Int? = null

    companion object {
        private const val WEATHER_REFRESH_MS = 1_800_000L
        private const val HOT_TEMP_C = 32.0
        private const val UV_HIGH = 6.0
        private const val WINDY_KMH = 25.0
        private const val FEELS_HOTTER_DIFF_C = 5.0
        private const val TTS_SPEED = 1.2f
        // Measured on this tablet+K29 speaker: a cold/suspended link takes ~1.1-1.4s from
        // AudioTrack.play() to the Bluetooth stack actually starting to transmit (A2DP has to
        // wake the link first); a link that's still warm from a recent previous greeting wakes
        // in under 100ms. Sized for the cold case with margin, since a warm-link greeting just
        // gets a bit more silence up front rather than risking the alternative (losing the
        // start of the sentence).
        private const val BT_WAKEUP_LEAD_MS = 1700
        // Extra wait after AudioTrack's local buffer reports fully drained, covering
        // Bluetooth A2DP's own downstream encode/transmit buffering before stop() is called -
        // without this the last syllable could still be clipped occasionally.
        private const val TRAILING_MARGIN_MS = 500L

        private val RAIN_CODES = setOf(51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99)
        private val WMO_DESC = mapOf(
            0 to "Trời quang", 1 to "Ít mây", 2 to "Có mây", 3 to "Nhiều mây",
            45 to "Sương mù", 48 to "Sương mù đóng băng",
            51 to "Mưa phùn nhẹ", 53 to "Mưa phùn", 55 to "Mưa phùn nặng",
            56 to "Mưa phùn đóng băng", 57 to "Mưa phùn đóng băng nặng",
            61 to "Mưa nhỏ", 63 to "Mưa vừa", 65 to "Mưa to",
            66 to "Mưa đóng băng", 67 to "Mưa đóng băng nặng",
            71 to "Tuyết nhẹ", 73 to "Tuyết vừa", 75 to "Tuyết to", 77 to "Mưa tuyết",
            80 to "Mưa rào nhẹ", 81 to "Mưa rào", 82 to "Mưa rào to",
            85 to "Mưa tuyết rào nhẹ", 86 to "Mưa tuyết rào to",
            95 to "Dông", 96 to "Dông kèm mưa đá nhẹ", 99 to "Dông kèm mưa đá to"
        )

        // type,category,template - category says which placeholders it needs (filtered out
        // when that data isn't available yet). Placeholders: {h} {name} {parts} {time_of_day}
        // {temp} {condition}. Edit this list directly to add/change wording.
        private val TEMPLATES = listOf(
            Triple("single", "normal", "Chào {h} {name}, chúc {h} một {time_of_day} vui vẻ"),
            Triple("single", "normal", "Xin chào {h} {name}, rất vui được gặp {h} hôm nay"),
            Triple("single", "temperature", "Chào {h} {name}, nhiệt độ Hà Nội hiện tại khoảng {temp} độ"),
            Triple("single", "weather", "Chào {h} {name}. {condition}"),
            Triple("pair", "normal", "Chào {parts}. Chúc mọi người một ngày mới vui vẻ"),
            Triple("pair", "normal", "Chào {parts}, rất vui được gặp mọi người hôm nay"),
            Triple("pair", "temperature", "Chào {parts}. Nhiệt độ Hà Nội hiện tại khoảng {temp} độ"),
            Triple("pair", "weather", "Chào {parts}. {condition}"),
        )
    }

    // ---- Weather ----

    private val weekdaysVn = arrayOf("Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy")
    private val dateFmt = SimpleDateFormat("dd/MM/yyyy", Locale.US)
    private val timeFmt = SimpleDateFormat("HH:mm:ss", Locale.US)

    /** Starts the periodic Hanoi weather fetch on a background thread - never blocks the
     * camera/UI threads, callers only ever read the already-fetched fields below. */
    fun startWeatherRefresh() {
        weatherExecutor.execute {
            try {
                while (true) {
                    try {
                        fetchWeather()
                    } catch (e: Exception) {
                        Log.e("GreetingTts", "weather fetch failed", e)
                    }
                    Thread.sleep(WEATHER_REFRESH_MS)
                }
            } catch (e: InterruptedException) {
                // release() called weatherExecutor.shutdownNow() (e.g. app closing/restarting)
                // while this loop was asleep - Runnable.run() can't declare a checked
                // exception, so letting InterruptedException escape here gets wrapped as an
                // uncaught java.lang.Error and crashes the whole process. This is the normal,
                // expected way this loop ends; just let the thread exit quietly.
            }
        }
    }

    private fun fetchWeather() {
        val uri = Uri.parse("https://api.open-meteo.com/v1/forecast").buildUpon()
            .appendQueryParameter("latitude", lat.toString())
            .appendQueryParameter("longitude", lon.toString())
            .appendQueryParameter("current", "temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,uv_index")
            .appendQueryParameter("daily", "temperature_2m_max,temperature_2m_min,weather_code")
            .appendQueryParameter("timezone", timezone)
            .build()
        val conn = URL(uri.toString()).openConnection() as HttpURLConnection
        conn.connectTimeout = 8000
        conn.readTimeout = 8000
        try {
            conn.connect()
            if (conn.responseCode != 200) {
                Log.e("GreetingTts", "Weather fetch failed: HTTP ${conn.responseCode}")
                return
            }
            val body = JSONObject(conn.inputStream.use { it.readBytes().toString(Charsets.UTF_8) })
            weather = body.optJSONObject("current")
            val daily = body.optJSONObject("daily")
            dailyMax = daily?.optJSONArray("temperature_2m_max")?.optDouble(0)
            dailyMin = daily?.optJSONArray("temperature_2m_min")?.optDouble(0)
            dailyCode = daily?.optJSONArray("weather_code")?.optInt(0)
            Log.e(
                "GreetingTts",
                "Weather updated ($locationName): ${weather?.optDouble("temperature_2m")}C " +
                    "(feels ${weather?.optDouble("apparent_temperature")}C), " +
                    "humidity ${weather?.optDouble("relative_humidity_2m")}%, " +
                    "today $dailyMin-${dailyMax}C, code ${weather?.optInt("weather_code")}"
            )
        } finally {
            conn.disconnect()
        }
    }

    fun locationName() = locationName

    fun dailyRangeText(): String {
        val max = dailyMax
        val min = dailyMin
        return if (max != null && min != null) "%.0f°C - %.0f°C".format(min, max) else "--"
    }

    fun weatherSummaryText(): String {
        val w = weather ?: return "Đang tải dữ liệu thời tiết..."
        val code = if (w.has("weather_code")) w.optInt("weather_code") else null
        val temp = if (w.has("temperature_2m")) w.optDouble("temperature_2m") else null
        val feels = if (w.has("apparent_temperature")) w.optDouble("apparent_temperature") else null
        val parts = mutableListOf<String>()
        WMO_DESC[code]?.let { parts.add(it) }
        temp?.let { parts.add("%.0f°C".format(it)) }
        if (feels != null && temp != null && kotlin.math.abs(feels - temp) >= 3) {
            parts.add("(cảm giác %.0f°C)".format(feels))
        }
        return if (parts.isNotEmpty()) parts.joinToString(", ") else "--"
    }

    fun dateTimeText(): String {
        val now = Calendar.getInstance()
        val weekday = weekdaysVn[now.get(Calendar.DAY_OF_WEEK) - 1]
        return "$weekday, ${dateFmt.format(now.time)} - ${timeFmt.format(now.time)}"
    }

    // ---- Greeting text ----

    private fun honorific(gender: String) = if (gender == "nu") "chị" else "anh"

    private fun timeOfDayPhrase(): String = when (Calendar.getInstance().get(Calendar.HOUR_OF_DAY)) {
        in 4..10 -> "buổi sáng"
        in 11..12 -> "buổi trưa"
        in 13..17 -> "buổi chiều"
        else -> "buổi tối"
    }

    private fun periodTodayPhrase(): String = when (Calendar.getInstance().get(Calendar.HOUR_OF_DAY)) {
        in 4..10 -> "sáng nay"
        in 11..17 -> "chiều nay"
        else -> "tối nay"
    }

    /** All health-reminder sentences that currently apply (rain, heat, high UV, humidity
     * making it feel hotter, strong wind) - usually one or two, sometimes none. [subject] is
     * "anh"/"chị" for one person or "mọi người" for two. */
    private fun weatherConditionSentences(subject: String): List<String> {
        val w = weather ?: return emptyList()
        val period = periodTodayPhrase()
        val code = if (w.has("weather_code")) w.optInt("weather_code") else null
        val temp = if (w.has("temperature_2m")) w.optDouble("temperature_2m") else null
        val feelsLike = if (w.has("apparent_temperature")) w.optDouble("apparent_temperature") else null
        val humidity = if (w.has("relative_humidity_2m")) w.optDouble("relative_humidity_2m") else null
        val uv = if (w.has("uv_index")) w.optDouble("uv_index") else null
        val wind = if (w.has("wind_speed_10m")) w.optDouble("wind_speed_10m") else null

        val sentences = mutableListOf<String>()
        if (code != null && RAIN_CODES.contains(code)) {
            sentences.add("Dự báo $period có mưa, chú ý mang theo ô khi ra ngoài để đảm bảo sức khỏe")
        }
        if (temp != null && temp >= HOT_TEMP_C) {
            sentences.add("Dự báo $period có nắng to, chú ý giữ gìn sức khỏe")
        }
        if (uv != null && uv >= UV_HIGH) {
            sentences.add("Chỉ số tia UV hôm nay khá cao, $subject nhớ che chắn khi ra ngoài trời")
        }
        if (feelsLike != null && temp != null && feelsLike - temp >= FEELS_HOTTER_DIFF_C) {
            sentences.add(
                "Nhiệt độ ngoài trời khoảng ${"%.0f".format(temp)} độ nhưng độ ẩm ${"%.0f".format(humidity ?: 0.0)}% " +
                    "khiến cảm giác như ${"%.0f".format(feelsLike)} độ, $subject chú ý giữ gìn sức khỏe"
            )
        }
        if (wind != null && wind >= WINDY_KMH) {
            sentences.add("Hôm nay gió khá mạnh, $subject chú ý khi di chuyển ngoài trời")
        }
        return sentences
    }

    private fun applicableTemplates(kind: String, haveTemp: Boolean, haveCondition: Boolean): List<String> {
        val pool = TEMPLATES.filter { it.first == kind }.map { it.third }
        val filtered = pool.filter {
            !(it.contains("{temp}") && !haveTemp) && !(it.contains("{condition}") && !haveCondition)
        }
        return filtered.ifEmpty { pool }
    }

    private fun pickTemplate(kind: String, haveTemp: Boolean, haveCondition: Boolean): String {
        val pool = applicableTemplates(kind, haveTemp, haveCondition)
        val candidates = if (pool.size > 1) pool.filter { it != lastTemplate[kind] } else pool
        val choice = candidates.random()
        lastTemplate[kind] = choice
        return choice
    }

    /** people: list of (name, gender). One person -> individual greeting; two -> combined.
     * Templates are picked at random, avoiding whichever was spoken last so it doesn't
     * repeat back-to-back. */
    fun buildGreeting(people: List<Pair<String, String>>): String {
        if (people.isEmpty()) return ""
        val kind = if (people.size == 1) "single" else "pair"
        val temp = if (weather?.has("temperature_2m") == true) weather!!.optDouble("temperature_2m") else null
        val context = mutableMapOf(
            "time_of_day" to timeOfDayPhrase(),
            "temp" to (temp?.let { "%.0f".format(it) } ?: "")
        )
        val subject: String
        if (kind == "single") {
            val (name, gender) = people[0]
            context["h"] = honorific(gender)
            context["name"] = name
            subject = context["h"]!!
        } else {
            context["parts"] = people.joinToString(" và ") { (n, g) -> "${honorific(g)} $n" }
            subject = "mọi người"
        }
        val conditions = weatherConditionSentences(subject)
        context["condition"] = if (conditions.isNotEmpty()) conditions.random() else ""

        val template = pickTemplate(kind, temp != null, conditions.isNotEmpty())
        return try {
            fillTemplate(template, context)
        } catch (e: Exception) {
            Log.e("GreetingTts", "template fill failed, using fallback", e)
            val fallback = TEMPLATES.first { it.first == kind && it.second == "normal" }.third
            fillTemplate(fallback, context)
        }
    }

    private fun fillTemplate(template: String, context: Map<String, String>): String {
        var result = template
        for ((key, value) in context) result = result.replace("{$key}", value)
        return result
    }

    // ---- TTS fetch/cache/play ----

    /** Fetches (or reuses cached) audio and plays it (sped up ~1.2x) on a background thread so
     * the camera loop never blocks on network I/O or decoding. [onDone] always fires eventually
     * (including on failure) so callers can safely clear a "now speaking" UI state.
     *
     * Decoding/playing goes through JLayer (pure-Java MP3 decode) + AudioTrack instead of
     * MediaPlayer/MediaCodec - this tablet's vendor ROM has a broken mediaswcodec APEX (missing
     * "libdirect-coredump.so" inside its own linker namespace, confirmed via logcat: the lib
     * exists in /system and /vendor but the sandboxed codec process can't see it), so
     * MediaPlayer.prepareAsync() on any MP3 just hangs forever with no callback ever firing.
     * Decoding ourselves sidesteps that broken system service entirely. */
    fun speak(text: String, onStart: () -> Unit = {}, onDone: () -> Unit = {}) {
        if (text.isBlank()) {
            onDone()
            return
        }
        executor.execute {
            val file = try {
                fetchOrCached(text)
            } catch (e: Exception) {
                Log.e("GreetingTts", "fetch failed", e)
                null
            }
            if (file == null) {
                mainHandler.post(onDone)
                return@execute
            }
            playFile(file, onStart, onDone)
        }
    }

    private fun fetchOrCached(text: String): File? {
        val hash = MessageDigest.getInstance("MD5").digest(text.toByteArray())
            .joinToString("") { "%02x".format(it.toInt() and 0xFF) }
        val file = File(cacheDir, "$hash.mp3")
        if (file.exists() && file.length() > 500) return file

        // translate_tts has an undocumented ~200-char practical limit per request. Its
        // ttsspeed param was tested and found to have no real effect (verified: identical
        // byte size/duration across values >=0.5) - speed-up happens client-side instead,
        // see speedUpPcm() below.
        val chunk = text.take(200)
        val q = URLEncoder.encode(chunk, "UTF-8")
        val url = URL("https://translate.googleapis.com/translate_tts?ie=UTF-8&q=$q&tl=vi&client=tw-ob")
        val conn = url.openConnection() as HttpURLConnection
        conn.setRequestProperty("User-Agent", "Mozilla/5.0")
        conn.connectTimeout = 8000
        conn.readTimeout = 8000
        try {
            conn.connect()
            if (conn.responseCode != 200) {
                Log.e("GreetingTts", "TTS fetch failed: HTTP ${conn.responseCode}")
                return null
            }
            val bytes = conn.inputStream.use { it.readBytes() }
            if (bytes.size < 500) {
                Log.e("GreetingTts", "TTS fetch returned suspiciously small body: ${bytes.size} bytes")
                return null
            }
            file.writeBytes(bytes)
            Log.e("GreetingTts", "TTS fetched OK: ${bytes.size} bytes -> ${file.name}")
            return file
        } finally {
            conn.disconnect()
        }
    }

    private class DecodedAudio(val samples: ShortArray, val sampleRate: Int, val channels: Int)

    /** Decodes an entire MP3 file to a single interleaved 16-bit PCM buffer via JLayer - fine
     * for short TTS clips (a few seconds), no need to stream frame-by-frame. */
    private fun decodeMp3ToPcm(file: File): DecodedAudio {
        val bitstream = Bitstream(FileInputStream(file))
        val decoder = Decoder()
        val chunks = mutableListOf<ShortArray>()
        var sampleRate = 44100
        var channels = 2
        try {
            while (true) {
                val header = bitstream.readFrame() ?: break
                val output = decoder.decodeFrame(header, bitstream) as SampleBuffer
                sampleRate = output.sampleFrequency
                channels = output.channelCount
                chunks.add(output.buffer.copyOfRange(0, output.bufferLength))
                bitstream.closeFrame()
            }
        } finally {
            bitstream.close()
        }
        val total = chunks.sumOf { it.size }
        val all = ShortArray(total)
        var pos = 0
        for (c in chunks) {
            System.arraycopy(c, 0, all, pos, c.size)
            pos += c.size
        }
        return DecodedAudio(all, sampleRate, channels)
    }

    /** Speeds up interleaved PCM by [factor] via linear-interpolation resampling (same effect
     * as the Windows tool's pygame.sndarray approach - changes pitch slightly, which reads as a
     * normal "sped up voice" effect and is fine for a short greeting). */
    private fun speedUpPcm(samples: ShortArray, channels: Int, factor: Float): ShortArray {
        if (factor == 1.0f || channels <= 0) return samples
        val frameCount = samples.size / channels
        val newFrameCount = (frameCount / factor).toInt().coerceAtLeast(1)
        val result = ShortArray(newFrameCount * channels)
        for (i in 0 until newFrameCount) {
            val srcPosF = i * factor
            val srcIdx = srcPosF.toInt().coerceIn(0, frameCount - 1)
            val nextIdx = (srcIdx + 1).coerceAtMost(frameCount - 1)
            val frac = srcPosF - srcIdx
            for (c in 0 until channels) {
                val a = samples[srcIdx * channels + c]
                val b = samples[nextIdx * channels + c]
                result[i * channels + c] = (a + (b - a) * frac).toInt().toShort()
            }
        }
        return result
    }

    /** translate_tts's source recording sits well below full scale, and the K29 Bluetooth
     * speaker in use can't be turned up any further (no physical volume control, and this
     * tablet fails to negotiate AVRCP absolute volume with it - confirmed via `dumpsys audio`
     * showing mAvrcpAbsVolSupported=false - so the tablet's own volume slider can't push the
     * speaker's amp any louder either). Peak-normalizing alone (scaling only until the single
     * loudest sample hits full scale) doesn't help much here since speech has a high crest
     * factor - most of the clip sits far quieter than that one peak. This instead drives the
     * gain off RMS (average loudness) and hard-limits anything that would clip, the same
     * "loudness maximizer" approach broadcast/mastering limiters use - trades a bit of
     * distortion on the loudest syllables for a real, audible increase in perceived volume. */
    private fun maximizeLoudness(samples: ShortArray, targetRmsRatio: Float = 0.35f, maxGain: Double = 6.0): ShortArray {
        if (samples.isEmpty()) return samples
        var sumSquares = 0.0
        for (s in samples) sumSquares += s.toDouble() * s.toDouble()
        val rms = kotlin.math.sqrt(sumSquares / samples.size)
        if (rms < 1.0) return samples
        val targetRms = targetRmsRatio * Short.MAX_VALUE
        val gain = (targetRms / rms).coerceAtMost(maxGain)
        if (gain <= 1.05) return samples // already loud enough
        val result = ShortArray(samples.size)
        var clipped = 0
        for (i in samples.indices) {
            val amplified = samples[i] * gain
            val clampedVal = amplified.coerceIn(Short.MIN_VALUE.toDouble(), Short.MAX_VALUE.toDouble())
            if (clampedVal != amplified) clipped++
            result[i] = clampedVal.toInt().toShort()
        }
        Log.e(
            "GreetingTts",
            "maximizeLoudness: rms=${"%.0f".format(rms)} -> gain=${"%.2f".format(gain)}x, " +
                "clipped ${"%.1f".format(100.0 * clipped / samples.size)}% of samples"
        )
        return result
    }

    /** Prepends silence so the greeting's actual speech doesn't start until [leadMs] into the
     * clip. Bluetooth A2DP has to "wake up" a suspended link before it actually transmits audio
     * (measured on this tablet+speaker: ~1.1s from AudioTrack.play() to "ON A2DP STARTED" in the
     * BT stack logs) - without this, that entire window plays into the local audio pipeline
     * before the link is actually carrying it, so the first ~1s of every greeting is silently
     * dropped and it sounds like the sentence starts mid-word or gets cut. */
    private fun padLeadingSilence(samples: ShortArray, sampleRate: Int, channels: Int, leadMs: Int): ShortArray {
        val leadFrames = (sampleRate.toLong() * leadMs / 1000L).toInt()
        val lead = ShortArray(leadFrames * channels) // zero-filled = silence
        return lead + samples
    }

    private fun stopAndRelease(track: AudioTrack) {
        // Benign race: two speak() calls close together (e.g. two people checking in at once)
        // can both reach here for the same track - IllegalStateException from double-stop/
        // release is expected in that case, not a real error.
        try {
            track.stop()
        } catch (e: IllegalStateException) {
            // already stopped/released - fine
        }
        try {
            track.release()
        } catch (e: IllegalStateException) {
            // already released - fine
        }
    }

    /** Runs entirely on the caller's thread (speak()'s single-thread executor) and blocks
     * until playback genuinely finishes - this is what makes back-to-back greetings queue up
     * and play one after another instead of racing: a second speak() call's executor task
     * can't start until this one returns, so it never gets the chance to stop() a still-
     * playing track out from under a greeting that hasn't finished yet (previously it did,
     * which is why "Nhiệt độ Hà Nội hiện tại khoảng 34 độ" could cut off mid-sentence when a
     * second greeting fired while the first was still talking). */
    private fun playFile(file: File, onStart: () -> Unit, onDone: () -> Unit) {
        var track: AudioTrack? = null
        try {
            val decoded = decodeMp3ToPcm(file)
            val gained = maximizeLoudness(speedUpPcm(decoded.samples, decoded.channels, TTS_SPEED))
            val sped = padLeadingSilence(gained, decoded.sampleRate, decoded.channels, BT_WAKEUP_LEAD_MS)
            val channelMask = if (decoded.channels >= 2) AudioFormat.CHANNEL_OUT_STEREO else AudioFormat.CHANNEL_OUT_MONO
            val bufferBytes = sped.size * 2
            val minBufSize = AudioTrack.getMinBufferSize(decoded.sampleRate, channelMask, AudioFormat.ENCODING_PCM_16BIT)
            track = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_MEDIA)
                        .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                        .build()
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .setSampleRate(decoded.sampleRate)
                        .setChannelMask(channelMask)
                        .build()
                )
                .setBufferSizeInBytes(maxOf(minBufSize, bufferBytes))
                .setTransferMode(AudioTrack.MODE_STATIC)
                .build()
            track.setVolume(1.0f)
            audioTrack = track

            Log.e(
                "GreetingTts",
                "playback starting: ${file.name} (${file.length()} bytes), " +
                    "pcm frames=${sped.size / decoded.channels} rate=${decoded.sampleRate} ch=${decoded.channels}"
            )
            track.write(sped, 0, sped.size)
            mainHandler.post(onStart)
            track.play()

            // Polling the real playback position instead of a blind fixed-duration sleep -
            // stopping right at (or before) the estimated duration was occasionally clipping
            // the last syllable, since AudioTrack's own local buffer can drain a bit slower
            // than the wall-clock estimate under load. A trailing margin on top accounts for
            // Bluetooth A2DP's own downstream encode/transmit buffering: reaching the end of
            // the local buffer doesn't mean the last bit of audio has actually reached the
            // speaker yet.
            val totalFrames = sped.size / decoded.channels
            val estDurationMs = (sped.size.toLong() * 1000L) / (decoded.sampleRate.toLong() * decoded.channels.toLong())
            val pollDeadlineMs = System.currentTimeMillis() + estDurationMs + 3000L
            while (System.currentTimeMillis() < pollDeadlineMs) {
                if (track.playbackHeadPosition >= totalFrames) break
                Thread.sleep(50)
            }
            Thread.sleep(TRAILING_MARGIN_MS)
        } catch (e: InterruptedException) {
            // release() was called (executor shutdown) - not a real error
        } catch (e: Exception) {
            Log.e("GreetingTts", "playback failed", e)
        } finally {
            track?.let { stopAndRelease(it) }
            if (audioTrack === track) audioTrack = null
            mainHandler.post(onDone)
        }
    }

    fun release() {
        audioTrack?.let {
            try { it.stop(); it.release() } catch (e: Exception) { Log.w("GreetingTts", "AudioTrack release failed", e) }
        }
        audioTrack = null
        executor.shutdown()
        weatherExecutor.shutdownNow()
    }
}
