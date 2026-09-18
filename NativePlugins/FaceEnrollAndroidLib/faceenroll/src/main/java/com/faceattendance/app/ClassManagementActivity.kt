package com.faceattendance.app

import android.app.AlertDialog
import android.content.Intent
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.EditText
import android.widget.FrameLayout
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import java.text.Collator
import java.util.Locale

/**
 * "Quản lý lớp" — entry point mới, thay MainActivity (màn camera) làm icon launcher chính của
 * FA. Bố cục 2 cột kiểu master-detail (giống ControlActivity: rail trái cố định + nội dung phải)
 * — cột trái là danh sách lớp (cuộn được nếu dài), bấm 1 lớp thì cột phải hiện danh sách học
 * sinh của đúng lớp đó, không chuyển hẳn sang màn khác. Dựng 100% bằng code (LinearLayout lồng
 * nhau, không RecyclerView/XML), cùng phong cách `showManageStudentsDialog()`/
 * `showStudentSamplesDialog()` đã có sẵn trong MainActivity.kt.
 *
 * Các thao tác thật (chụp camera, chọn ảnh từ thư viện, xem/xoá từng mẫu ảnh) đều giao lại cho
 * MainActivity qua Intent tường minh — không viết lại pipeline nhận diện/enroll ở đây.
 *
 * Bảng màu: nền sáng, đồng bộ với Launcher (D:\X_projects\Launcher) — xem hằng số màu bên dưới.
 */
class ClassManagementActivity : AppCompatActivity() {

    private lateinit var attendanceStore: AttendanceStore

    private lateinit var addClassBtn: TextView
    private lateinit var classRailList: LinearLayout
    private lateinit var rightHeader: LinearLayout
    private lateinit var rightHeaderTitle: TextView
    private lateinit var renameBtn: TextView
    private lateinit var actionButtonsRow: LinearLayout
    private lateinit var dupWarningBadge: TextView
    private lateinit var viewModeBtn: TextView
    private lateinit var sortAliasBtn: TextView
    private lateinit var sortRealBtn: TextView
    private lateinit var scroll: ScrollView

    /** Lớp đang chọn ở cột trái — null nếu chưa có lớp nào (hoặc chưa tạo lớp nào cả). */
    private var currentClass: String? = null

    /** "grid" (lưới ảnh, mặc định) hoặc "list" (danh sách hàng ngang, tên thường gọi/tên thật
     * tách cột) — xem nút viewModeBtn. */
    private var viewMode: String = "grid"
    /** "alias" (tên thường gọi) hoặc "real" (tên thật) — quyết định sortedRoster() sắp theo cột
     * nào, và cột nào được hiện thành tên chính (in đậm) ở cả 2 chế độ xem. */
    private var sortMode: String = "alias"
    /** true = A→Z, false = Z→A — bấm lại đúng nút đang active (sortAliasBtn/sortRealBtn) thì đảo
     * chiều; bấm sang nút còn lại thì luôn reset về A→Z. */
    private var sortAscending: Boolean = true

    /** Nhóm học sinh (theo tên thật) bị trùng tên thường gọi trong lớp đang xem — set lại mỗi
     * lần renderRightPanel(), đọc lại khi bấm dupWarningBadge để hiện dialog đề xuất đổi tên. */
    private var currentDupGroups: List<List<String>> = emptyList()

    // Vietnamese-locale, accent/case-insensitive collator — dùng để sắp xếp theo TÊN (âm tiết
    // cuối), không phải theo cả chuỗi, vì tên đệm/họ đứng trước không quyết định thứ tự alphabet
    // theo cách gọi tên quen thuộc (vd "Khánh Vy" xếp theo "Vy", không phải "Khánh").
    private val vnCollator: Collator = Collator.getInstance(Locale.forLanguageTag("vi")).apply {
        strength = Collator.PRIMARY
    }
    /** Tách thành list từ cuối lên đầu: "Nguyễn Văn An" -> ["An", "Văn", "Nguyễn"]. So khớp danh
     * sách đã đảo này theo từng vị trí (âm tiết TÊN trước, rồi tới đệm, rồi tới họ) là cách sắp
     * xếp tên Việt Nam đúng chuẩn — so y hệt kiểu 1 chuỗi cắt lấy âm tiết cuối sẽ SAI khi 2 tên
     * trùng âm tiết cuối nhưng khác đệm (vd "Vũ Minh An" và "Nguyễn Văn An" đều tận cùng "An" —
     * cắt 1 âm tiết sẽ coi 2 tên này ngang hàng rồi phá hoà bằng cả chuỗi, tức sắp theo HỌ thay vì
     * theo TÊN/đệm, kết quả "...Văn An" nhảy lên trước "Minh An" một cách vô lý). */
    private fun reversedWords(s: String): List<String> =
        s.trim().split(Regex("\\s+")).filter { it.isNotEmpty() }.asReversed()

    /** Sắp theo tên thường gọi hoặc tên thật (tuỳ sortMode), chiều A→Z hoặc Z→A (tuỳ
     * sortAscending) — xem reversedWords() ở trên cho lý do so theo từng âm tiết từ cuối lên. */
    private fun sortedRoster(names: List<String>): List<String> {
        val keyOf: (String) -> String = if (sortMode == "real") { n -> n } else { n -> attendanceStore.aliasOf(n) }
        val cmp = Comparator<String> { a, b ->
            val wa = reversedWords(keyOf(a))
            val wb = reversedWords(keyOf(b))
            var result = 0
            for (i in 0 until minOf(wa.size, wb.size)) {
                result = vnCollator.compare(wa[i], wb[i])
                if (result != 0) break
            }
            if (result == 0) result = wa.size - wb.size
            if (sortAscending) result else -result
        }
        return names.sortedWith(cmp)
    }

    /** Nhóm các học sinh (theo tên thật) có tên thường gọi trùng nhau trong cùng danh sách. */
    private fun findDuplicateAliasGroups(names: List<String>): List<List<String>> =
        names.groupBy { attendanceStore.aliasOf(it) }.values.filter { it.size > 1 }

    /** Đề xuất tên thường gọi phân biệt khi trùng: Họ + Tên (bỏ tên đệm ở giữa), ví dụ
     * "Nguyễn Văn An" / "Lê Văn An" cùng có tên thường gọi mặc định "Văn An" -> đề xuất
     * "Nguyễn An" / "Lê An". */
    private fun suggestedAlias(realName: String): String {
        val parts = realName.trim().split(Regex("\\s+")).filter { it.isNotEmpty() }
        return if (parts.size >= 2) "${parts.first()} ${parts.last()}" else realName
    }

    private val dp get() = resources.displayMetrics.density
    private fun px(v: Int) = (v * dp).toInt()

    // ── Bảng màu sáng, cùng token với Launcher/ControlActivity ─────────────────────────────
    private val cBg        = Color.parseColor("#F5F1E8")
    private val cRail       = Color.parseColor("#F8F5EC")
    private val cCard       = Color.parseColor("#FFFFFF")
    private val cCardLine   = Color.parseColor("#E5DFD0")
    private val cText       = Color.parseColor("#2A2338")
    private val cTextDim    = Color.parseColor("#6E6580")
    private val cTextFaint  = Color.parseColor("#9990A8")
    private val cAccent     = Color.parseColor("#5B3F9E")
    private val cAccentDim  = Color.parseColor("#EFE9FB")
    private val cWarn       = Color.parseColor("#C98A1F")

    private val cMale   = 0xFF3E7FD1.toInt() // xanh blue — nam
    private val cFemale = 0xFFB15FD1.toInt() // tím nhạt/hồng — nữ
    private fun colorFor(gender: String): Int = if (gender == "nu") cFemale else cMale
    private fun initialOf(name: String): String =
        name.trim().firstOrNull()?.uppercaseChar()?.toString() ?: "?"

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        attendanceStore = AttendanceStore(this)
        seedDemoDataIfEmpty()
        setContentView(buildRoot())
        refreshAll()
    }

    /** Nhúng sẵn 1 danh sách lớp/học sinh giả định (Mầm/Chồi/Lá) để màn "Quản lý lớp" có nội
     * dung xem ngay, không cần chụp ảnh tay từng bạn trước. Kiểm tra bằng 1 học sinh mốc (thay vì
     * "chưa có lớp nào") — máy nào đã từng test tạo lớp/học sinh thật trước đó trong phiên này
     * vẫn được nhúng thêm danh sách giả định, không bị guard chặn im lặng chỉ vì đã có SẴN dữ
     * liệu khác. Mọi thao tác bên trong đều idempotent (ensurePlaceholder/setGender/setAlias/
     * setClassName ghi đè cùng giá trị nếu gọi lại) nên an toàn khi hàm này chạy lại nhiều lần —
     * guard ở đây chỉ để tránh ghi đĩa lãng phí mỗi lần mở màn hình, không phải để đảm bảo đúng.
     * Học sinh giả định = 0 mẫu ảnh (hiện "Cần ảnh" như bình thường) vì không có ảnh thật để gán.
     * TODO: cân nhắc bỏ/tắt hàm này trước khi build bản triển khai thật cho trường. */
    private fun seedDemoDataIfEmpty() {
        if (attendanceStore.classNameOf("Nguyễn Khánh Vy") != null) return
        data class Seed(val real: String, val alias: String, val gender: String)
        val mam = listOf(
            Seed("Nguyễn Bảo An", "Bảo An", "nam"),
            Seed("Trần Gia Hân", "Gia Hân", "nu"),
        )
        val choi = listOf(
            Seed("Lê Minh Khôi", "Minh Khôi", "nam"),
            Seed("Phạm Yến Nhi", "Yến Nhi", "nu"),
            Seed("Vũ Đăng Khoa", "Đăng Khoa", "nam"),
        )
        // Danh sách lớp Lá — đồng bộ đúng bộ tên ví dụ đã dùng ở bản xem trước "Giao Diện K02".
        val la = listOf(
            Seed("Nguyễn Khánh Vy", "Khánh Vy", "nu"), Seed("Trần Nam Khang", "Nam Khang", "nam"),
            Seed("Lê Bảo Châu", "Bảo Châu", "nu"), Seed("Phạm Tuấn Kiệt", "Tuấn Kiệt", "nam"),
            Seed("Đỗ Thảo My", "Thảo My", "nu"), Seed("Vũ Minh An", "Minh An", "nam"),
            Seed("Hoàng Gia Hân", "Gia Hân", "nu"), Seed("Bùi Bảo Ngọc", "Bảo Ngọc", "nu"),
            Seed("Ngô Hoàng Long", "Hoàng Long", "nam"), Seed("Dương Thanh Trúc", "Thanh Trúc", "nu"),
            Seed("Nguyễn Văn An", "Văn An", "nam"), Seed("Lê Văn An", "Văn An", "nam"),
            Seed("Trần Ngọc Hà", "Ngọc Hà", "nu"), Seed("Phạm Quang Huy", "Quang Huy", "nam"),
            Seed("Vũ Bảo Trâm", "Bảo Trâm", "nu"), Seed("Hoàng Minh Thư", "Minh Thư", "nu"),
            Seed("Đặng Gia Bảo", "Gia Bảo", "nam"), Seed("Lý Khôi Nguyên", "Khôi Nguyên", "nam"),
            Seed("Ngô Yến Nhi", "Yến Nhi", "nu"), Seed("Nguyễn Anh Thư", "Anh Thư", "nu"),
            Seed("Trần Hải Đăng", "Hải Đăng", "nam"), Seed("Lê Tường Vy", "Tường Vy", "nu"),
            Seed("Bùi Đăng Khoa", "Đăng Khoa", "nam"), Seed("Dương Phương Linh", "Phương Linh", "nu"),
            Seed("Phạm Nhật Minh", "Nhật Minh", "nam"), Seed("Vũ Kim Ngân", "Kim Ngân", "nu"),
            Seed("Đặng Quốc Bảo", "Quốc Bảo", "nam"), Seed("Hoàng Diệu Anh", "Diệu Anh", "nu"),
            Seed("Lý Thiên Ân", "Thiên Ân", "nam"), Seed("Ngô Mai Chi", "Mai Chi", "nu"),
        )
        for ((className, roster) in listOf("Lớp Mầm" to mam, "Lớp Chồi" to choi, "Lớp Lá" to la)) {
            attendanceStore.addClass(className)
            for (s in roster) {
                attendanceStore.ensurePlaceholder(s.real)
                attendanceStore.setGender(s.real, s.gender)
                attendanceStore.setAlias(s.real, s.alias)
                attendanceStore.setClassName(s.real, className)
            }
        }
    }

    override fun onResume() {
        super.onResume()
        // Quay lại từ MainActivity (vừa enroll/xoá/đổi tên...) — đọc lại dữ liệu từ đĩa và vẽ
        // lại toàn bộ (rail + nội dung lớp đang chọn).
        attendanceStore = AttendanceStore(this)
        refreshAll()
    }

    /** Vẽ lại rail (danh sách lớp) + panel phải (học sinh của lớp đang chọn), giữ nguyên lựa
     * chọn hiện tại nếu lớp đó vẫn còn tồn tại; nếu không thì tự chọn lớp đầu tiên. */
    private fun refreshAll() {
        val classes = attendanceStore.allClassNames()
        if (currentClass !in classes) currentClass = classes.firstOrNull()
        renderClassRail(classes)
        renderRightPanel()
    }

    // ─────────────────────────────────────────────────────────────────────────  layout

    private fun buildRoot(): View {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(cBg)
        }

        // Chữ "Quản lý lớp" đẩy kịch lề trái (không còn căn theo bề rộng rail bên dưới nữa) +
        // nút "+ Thêm lớp" ngay cạnh nó — logo đẩy hẳn sang phải, đồng bộ bố cục với top bar của
        // ControlActivity (logo luôn ở góc phải), tránh logo to đè lên chữ tiêu đề.
        val header = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(px(20), px(16), px(16), px(14))
        }
        header.addView(TextView(this).apply {
            text = "Quản lý lớp"
            textSize = 22f
            setTypeface(typeface, Typeface.BOLD)
            setTextColor(cText)
        })
        addClassBtn = pillButton("+ Thêm lớp") { promptAddClass() }
        addClassBtn.layoutParams = LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT
        ).also { it.marginStart = px(8) }
        header.addView(addClassBtn)
        header.addView(View(this).apply {
            layoutParams = LinearLayout.LayoutParams(0, 0, 1f) // đẩy logo sát lề phải
        })
        header.addView(ImageView(this).apply {
            setImageResource(R.drawable.logo_eduxplore)
            adjustViewBounds = true
            scaleType = ImageView.ScaleType.FIT_END
            layoutParams = LinearLayout.LayoutParams(px(120), px(46))
        })
        root.addView(header)

        val body = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f)
        }
        root.addView(body)

        // Cột trái: danh sách lớp — cố định bề rộng, cuộn được nếu dài (giống rail của
        // ControlActivity, xem activity_control.xml action_zone).
        val classRailScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(px(220), ViewGroup.LayoutParams.MATCH_PARENT)
            setBackgroundColor(cRail)
        }
        classRailList = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(px(12), px(12), px(12), px(12))
        }
        classRailScroll.addView(classRailList)
        body.addView(classRailScroll)

        body.addView(View(this).apply {
            layoutParams = LinearLayout.LayoutParams(px(1), ViewGroup.LayoutParams.MATCH_PARENT)
            setBackgroundColor(cCardLine)
        })

        // Cột phải: học sinh của lớp đang chọn.
        val rightCol = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1f)
        }
        body.addView(rightCol)

        // Tất cả trên 1 hàng: tên lớp (bấm để đổi tên) + icon bút chì, để dành không gian cho tên
        // lớp dài — 3 nút phụ + cảnh báo trùng tên dồn hẳn sát lề phải cạnh nhau bằng spacer.
        rightHeader = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(px(20), px(14), px(20), px(14))
        }
        rightHeaderTitle = TextView(this).apply {
            textSize = 20f
            setTypeface(typeface, Typeface.BOLD)
            setTextColor(cText)
            isClickable = true
            isFocusable = true
            setPadding(px(4), px(4), px(4), px(4))
            setOnClickListener { currentClass?.let { promptRenameClass(it) } }
        }
        rightHeader.addView(rightHeaderTitle)
        renameBtn = TextView(this).apply {
            text = "✏"
            textSize = 15f
            setTextColor(cAccent)
            setPadding(px(6), px(6), px(6), px(6))
            rotation = 135f // glyph render nằm ngang trên font máy — xoay chéo cho giống bút chì thật (135, không phải -45, vì -45 làm đầu bút lộn ngược)
            setOnClickListener { currentClass?.let { promptRenameClass(it) } }
        }
        rightHeader.addView(renameBtn)
        rightHeader.addView(View(this).apply {
            layoutParams = LinearLayout.LayoutParams(0, 0, 1f) // đẩy 3 nút phụ + cảnh báo sát lề phải
        })
        actionButtonsRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        actionButtonsRow.addView(smallActionButton("+ Học sinh mới") { onAddStudentClicked() })
        actionButtonsRow.addView(smallActionButton("Thêm từ camera") { onAddStudentClicked() })
        actionButtonsRow.addView(smallActionButton("Thêm từ ảnh") { onBulkAddClicked() })
        actionButtonsRow.addView(smallActionButton("Từ DS đã có") { onAssignExistingClicked() })
        rightHeader.addView(actionButtonsRow)
        // Cảnh báo trùng tên thường gọi trong lớp — ngay cạnh 3 nút phụ, sát góc trên-phải, ẩn khi
        // không có trùng. Bấm vào hiện dialog đề xuất đổi tên phân biệt (xem showDuplicateAliasDialog).
        dupWarningBadge = TextView(this).apply {
            text = "⚠"
            textSize = 18f
            setTextColor(cWarn)
            setPadding(px(8), px(6), 0, px(6))
            visibility = View.GONE
            setOnClickListener { showDuplicateAliasDialog() }
        }
        rightHeader.addView(dupWarningBadge)
        rightCol.addView(rightHeader)

        // Hàng công cụ phụ: chuyển lưới/danh sách + chọn cột sắp xếp — tách riêng khỏi rightHeader
        // (đã khá đầy) thành 1 hàng mỏng ngay trên vùng cuộn danh sách học sinh.
        val listControlsRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(px(20), 0, px(20), px(10))
        }
        viewModeBtn = pillButton("☰ Xem dạng danh sách") { toggleViewMode() }
        listControlsRow.addView(viewModeBtn)
        listControlsRow.addView(View(this).apply {
            layoutParams = LinearLayout.LayoutParams(px(18), ViewGroup.LayoutParams.WRAP_CONTENT)
        })
        listControlsRow.addView(TextView(this).apply {
            text = "Sắp xếp:"
            textSize = 12f
            setTextColor(cTextFaint)
            setPadding(0, 0, px(8), 0)
        })
        sortAliasBtn = sortToggleButton("Tên thường gọi") { setSortMode("alias") }
        sortRealBtn = sortToggleButton("Tên thật") { setSortMode("real") }
        listControlsRow.addView(sortAliasBtn)
        listControlsRow.addView(sortRealBtn)
        rightCol.addView(listControlsRow)

        scroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f)
        }
        rightCol.addView(scroll)
        return root
    }

    private fun pillButton(label: String, onClick: () -> Unit): TextView = TextView(this).apply {
        text = label
        setTextColor(cAccent)
        textSize = 14f
        setTypeface(typeface, Typeface.BOLD)
        setPadding(px(12), px(8), px(12), px(8))
        setOnClickListener { onClick() }
    }

    /** Nút dạng segmented-control (2 lựa chọn cạnh nhau, 1 cái luôn "active") — dùng cho chọn cột
     * sắp xếp. Trạng thái active/inactive được set lại mỗi renderRightPanel() qua styleSortToggle(). */
    private fun sortToggleButton(label: String, onClick: () -> Unit): TextView = TextView(this).apply {
        text = label
        textSize = 11.5f
        setTypeface(typeface, Typeface.BOLD)
        setPadding(px(10), px(6), px(10), px(6))
        layoutParams = LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT
        ).also { it.marginEnd = px(6) }
        setOnClickListener { onClick() }
    }

    private fun styleSortToggle(v: TextView, active: Boolean) {
        v.setTextColor(if (active) Color.WHITE else cAccent)
        v.background = GradientDrawable().apply {
            setColor(if (active) cAccent else Color.TRANSPARENT)
            setStroke(px(1), cAccent)
            cornerRadius = px(8).toFloat()
        }
    }

    private fun toggleViewMode() {
        viewMode = if (viewMode == "grid") "list" else "grid"
        renderRightPanel()
    }

    private fun setSortMode(mode: String) {
        if (sortMode == mode) {
            sortAscending = !sortAscending // bấm lại đúng cột đang sort -> đảo chiều A-Z/Z-A
        } else {
            sortMode = mode
            sortAscending = true // đổi sang cột khác -> luôn bắt đầu lại từ A-Z
        }
        renderRightPanel()
    }

    /** Nút nhỏ, gọn cho chức năng phụ (thêm học sinh/từ camera/từ ảnh) — không stretch full-width
     * như Button mặc định, chỉ vừa đủ chữ, xếp cạnh nhau ở cuối hàng tiêu đề lớp. */
    private fun smallActionButton(label: String, onClick: () -> Unit): TextView = TextView(this).apply {
        text = label
        textSize = 13.5f
        setTypeface(typeface, Typeface.BOLD)
        setTextColor(Color.WHITE)
        background = GradientDrawable().apply {
            setColor(cAccent)
            cornerRadius = px(8).toFloat()
        }
        setPadding(px(12), px(8), px(12), px(8))
        layoutParams = LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT
        ).also { it.marginStart = px(6) }
        setOnClickListener { onClick() }
    }

    /** Xếp items thành lưới N cột bằng LinearLayout lồng nhau (không cần RecyclerView/GridLayout
     * cho vài chục item — cùng phong cách "build View bằng tay" đã dùng trong MainActivity). */
    private fun buildGrid(items: List<View>, columns: Int = 3): View {
        val container = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        var i = 0
        while (i < items.size) {
            val row = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
                layoutParams = LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT
                ).also { it.topMargin = px(4) }
            }
            for (c in 0 until columns) {
                val cell = FrameLayout(this).apply {
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).also {
                        if (c < columns - 1) it.marginEnd = px(10)
                    }
                }
                if (i < items.size) {
                    // Card phải fill hết bề rộng cell (MATCH_PARENT) — nếu không, FrameLayout mặc
                    // định wrap_content theo nội dung riêng của từng card (tên dài/ngắn khác nhau),
                    // khiến các card trong cùng 1 hàng to nhỏ lệch nhau dù cell đã đều nhau.
                    items[i].layoutParams = FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT
                    )
                    cell.addView(items[i])
                    i++
                }
                row.addView(cell)
            }
            container.addView(row)
        }
        return container
    }

    private fun emptyState(msg: String): View = TextView(this).apply {
        text = msg
        setTextColor(cTextFaint)
        textSize = 14f
        gravity = Gravity.CENTER
        setPadding(px(40), px(60), px(40), px(60))
    }

    private fun card(onClick: () -> Unit): LinearLayout = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(px(14), px(14), px(14), px(14))
        background = GradientDrawable().apply {
            setColor(cCard)
            setStroke(px(1), cCardLine)
            cornerRadius = px(14).toFloat()
        }
        elevation = px(2).toFloat()
        isClickable = true
        isFocusable = true
        setOnClickListener { onClick() }
    }

    /** Circular avatar: the student's representative sample photo if one exists, otherwise a
     * colored circle with their name's initial (same "no photo yet" look the roster/photo grids
     * use to flag someone as needing a picture). */
    private fun avatarWithInitial(name: String, sizeDp: Int, photoPath: String?, gender: String): View {
        val sizePx = px(sizeDp)
        val bmp = photoPath?.let { attendanceStore.loadPhotoBitmap(it, sizePx) }
        val frame = FrameLayout(this).apply {
            layoutParams = LinearLayout.LayoutParams(sizePx, sizePx)
        }
        frame.addView(ImageView(this).apply {
            layoutParams = FrameLayout.LayoutParams(sizePx, sizePx)
            scaleType = ImageView.ScaleType.CENTER_CROP
            background = GradientDrawable().apply {
                shape = GradientDrawable.OVAL
                setColor(if (bmp != null) cCardLine else colorFor(gender))
            }
            clipToOutline = true
            if (bmp != null) setImageBitmap(bmp)
        })
        if (bmp == null) {
            frame.addView(TextView(this).apply {
                text = initialOf(name)
                setTextColor(Color.WHITE)
                textSize = sizeDp * 0.36f
                setTypeface(typeface, Typeface.BOLD)
                gravity = Gravity.CENTER
                layoutParams = FrameLayout.LayoutParams(sizePx, sizePx)
            })
        }
        return frame
    }

    // ─────────────────────────────────────────────────────────────────────────  render

    private fun renderClassRail(classes: List<String>) {
        classRailList.removeAllViews()
        if (classes.isEmpty()) {
            classRailList.addView(TextView(this).apply {
                text = "Chưa có lớp nào.\nBấm \"+ Thêm lớp\" ở trên."
                setTextColor(cTextFaint)
                textSize = 12.5f
                setPadding(px(6), px(10), px(6), px(10))
            })
            return
        }
        classes.forEachIndexed { i, className ->
            val students = attendanceStore.studentsInClass(className)
            val needing = students.count { attendanceStore.samplesOf(it).isEmpty() }
            val selected = className == currentClass
            val row = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(px(12), px(10), px(12), px(10))
                background = GradientDrawable().apply {
                    setColor(if (selected) cAccentDim else Color.TRANSPARENT)
                    cornerRadius = px(10).toFloat()
                }
                isClickable = true
                isFocusable = true
                layoutParams = LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT
                ).also { it.bottomMargin = px(4) }
                setOnClickListener {
                    currentClass = className
                    renderClassRail(classes)
                    renderRightPanel()
                }
            }
            row.addView(TextView(this).apply {
                text = "${i + 1}. $className"
                setTextColor(if (selected) cAccent else cText)
                textSize = 14f
                setTypeface(typeface, Typeface.BOLD)
                maxLines = 1
            })
            row.addView(TextView(this).apply {
                text = "${students.size} học sinh" + if (needing > 0) " · $needing cần ảnh" else ""
                setTextColor(if (needing > 0) cWarn else cTextDim)
                textSize = 11.5f
                setPadding(0, px(2), 0, 0)
            })
            classRailList.addView(row)
        }
    }

    private fun renderRightPanel() {
        val className = currentClass
        if (className == null) {
            rightHeaderTitle.text = "Chưa có lớp nào"
            renameBtn.visibility = View.GONE
            actionButtonsRow.visibility = View.GONE
            scroll.removeAllViews()
            scroll.addView(emptyState("Bấm \"+ Thêm lớp\" ở góc trên để tạo lớp đầu tiên."))
            return
        }

        rightHeaderTitle.text = className
        renameBtn.visibility = View.VISIBLE
        actionButtonsRow.visibility = View.VISIBLE

        viewModeBtn.text = if (viewMode == "grid") "☰ Xem dạng danh sách" else "▦ Xem dạng lưới"
        val dirArrow = if (sortAscending) " A→Z" else " Z→A"
        sortAliasBtn.text = "Tên thường gọi" + if (sortMode == "alias") dirArrow else ""
        sortRealBtn.text = "Tên thật" + if (sortMode == "real") dirArrow else ""
        styleSortToggle(sortAliasBtn, sortMode == "alias")
        styleSortToggle(sortRealBtn, sortMode == "real")

        val students = sortedRoster(attendanceStore.studentsInClass(className))
        currentDupGroups = findDuplicateAliasGroups(students)
        dupWarningBadge.visibility = if (currentDupGroups.isNotEmpty()) View.VISIBLE else View.GONE
        scroll.removeAllViews()
        scroll.addView(
            when {
                students.isEmpty() -> emptyState("Lớp \"$className\" chưa có học sinh nào.\nDùng các nút bên trên để thêm.")
                viewMode == "list" -> buildListView(students)
                else -> buildGrid(students.mapIndexed { i, s -> studentCardView(s, i) }, 5)
            }
        )
    }

    /** Danh sách hàng ngang, 1 học sinh/hàng: STT, tên thường gọi, tên thật, ghi chú ảnh mẫu —
     * dành cho lớp đông (mục tiêu 30 học sinh), dễ rà soát/đối chiếu hơn lưới ảnh. */
    private fun buildListView(students: List<String>): View {
        val emphasizeReal = sortMode == "real" // cột nào đang là "tên chính" (in đậm) - xem sortedRoster()/setSortMode()
        val container = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        container.addView(listRow("STT", "Tên thường gọi", "Tên thật", "Ảnh mẫu", header = true, emphasizeReal = emphasizeReal))
        students.forEachIndexed { i, name ->
            val samples = attendanceStore.samplesOf(name)
            val needsUpdate = samples.isEmpty()
            val alias = attendanceStore.aliasOf(name)
            container.addView(
                listRow(
                    "${i + 1}", alias, if (alias != name) name else "—",
                    if (needsUpdate) "Cần thêm ảnh" else "${samples.size} ảnh",
                    warn = needsUpdate,
                    onClick = { openStudent(name) },
                    zebra = i % 2 == 0,
                    emphasizeReal = emphasizeReal
                )
            )
        }
        return container
    }

    private fun listCell(text: String, weight: Float, bold: Boolean, color: Int, sizeSp: Float): TextView =
        TextView(this).apply {
            this.text = text
            setTextColor(color)
            textSize = sizeSp
            if (bold) setTypeface(typeface, Typeface.BOLD)
            maxLines = 1
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, weight)
        }

    /** emphasizeReal quyết định cột nào là "tên chính" (in đậm, màu chữ chính) — mặc định tên
     * thường gọi là chính; bấm nút sort "Tên thật" thì đảo lại, tên thật thành chính (xem
     * "click vào tên thường gọi/tên thật thì hiện thành tên chính" trong yêu cầu). */
    private fun listRow(
        stt: String, alias: String, realName: String, note: String,
        header: Boolean = false, warn: Boolean = false, zebra: Boolean = false,
        emphasizeReal: Boolean = false, onClick: (() -> Unit)? = null
    ): View = LinearLayout(this).apply {
        orientation = LinearLayout.HORIZONTAL
        gravity = Gravity.CENTER_VERTICAL
        setPadding(px(10), if (header) px(4) else px(9), px(10), if (header) px(6) else px(9))
        if (!header) {
            background = GradientDrawable().apply {
                setColor(if (zebra) cCard else Color.TRANSPARENT)
                cornerRadius = px(8).toFloat()
            }
        }
        if (onClick != null) { isClickable = true; isFocusable = true; setOnClickListener { onClick() } }
        val dimColor = if (header) cTextFaint else cTextDim
        val mainColor = if (header) cTextFaint else cText
        val aliasBold = header || !emphasizeReal
        val realBold = !header && emphasizeReal
        val aliasColor = if (!header && emphasizeReal) dimColor else mainColor
        val realColor = if (!header && emphasizeReal) mainColor else dimColor
        addView(listCell(stt, 0.6f, header, dimColor, if (header) 11.5f else 13f))
        addView(listCell(alias, 2f, aliasBold, aliasColor, if (header) 11.5f else 13.5f))
        addView(listCell(realName, 2f, realBold, realColor, if (header) 11.5f else 13.5f))
        addView(listCell(note, 1.4f, header, if (warn) cWarn else dimColor, if (header) 11.5f else 12f))
    }

    /** Hiện dialog liệt kê từng nhóm trùng tên thường gọi + đề xuất đổi tên phân biệt (Họ + Tên,
     * bỏ tên đệm), cho phép áp dụng hàng loạt luôn. */
    private fun showDuplicateAliasDialog() {
        val groups = currentDupGroups
        if (groups.isEmpty()) return
        val msg = StringBuilder()
        for (group in groups) {
            val alias = attendanceStore.aliasOf(group.first())
            msg.append("\"$alias\" trùng giữa: ${group.joinToString(", ")}\n")
            msg.append("Đề xuất: ${group.joinToString(" · ") { suggestedAlias(it) }}\n\n")
        }
        AlertDialog.Builder(this)
            .setTitle("Tên thường gọi bị trùng")
            .setMessage(msg.toString().trim())
            .setPositiveButton("Áp dụng đề xuất") { _, _ ->
                for (group in groups) {
                    for (realName in group) attendanceStore.setAlias(realName, suggestedAlias(realName))
                }
                renderRightPanel()
            }
            .setNegativeButton("Để sau", null)
            .show()
    }

    private fun studentCardView(name: String, index: Int): View {
        val samples = attendanceStore.samplesOf(name)
        val needsUpdate = samples.isEmpty()
        val alias = attendanceStore.aliasOf(name)
        // Tên chính hiện trên card đổi theo sortMode — bấm nút sort "Tên thật" thì tên thật lên
        // thành tên chính thay vì tên thường gọi (đồng bộ với buildListView()'s emphasizeReal).
        val primaryName = if (sortMode == "real") name else alias
        return card { openStudent(name) }.apply {
            gravity = Gravity.CENTER_HORIZONTAL
            addView(TextView(this@ClassManagementActivity).apply {
                text = "${index + 1}"
                setTextColor(cTextDim)
                textSize = 15f
                setTypeface(typeface, Typeface.BOLD)
            })
            addView(avatarWithInitial(primaryName, 60, attendanceStore.representativePhoto(name), attendanceStore.genderOf(name)).also {
                (it.layoutParams as? ViewGroup.MarginLayoutParams)?.topMargin = px(4)
                it.setPadding(0, px(4), 0, px(8))
            })
            addView(TextView(this@ClassManagementActivity).apply {
                text = primaryName
                setTextColor(cText)
                textSize = 13.5f
                setTypeface(typeface, Typeface.BOLD)
                gravity = Gravity.CENTER
                // Tên thật dài hơn tên thường gọi nhiều (vd "Nguyễn Khánh Vy" so với "Khánh Vy") —
                // 1 dòng bị cắt cụt mất nửa tên. Cho phép 2 dòng, NHƯNG minLines=2 luôn giữ đúng 2
                // dòng dù tên ngắn chỉ cần 1 dòng — để mọi card cùng hàng cao đều nhau, tránh lệch
                // trục Y như trước (xem buildGrid()'s MATCH_PARENT fix).
                maxLines = 2
                minLines = 2
                ellipsize = android.text.TextUtils.TruncateAt.END
                setLineSpacing(0f, 1f)
            })
            addView(TextView(this@ClassManagementActivity).apply {
                text = if (needsUpdate) "Cần cập nhật ảnh" else "${samples.size} mẫu ảnh"
                setTextColor(if (needsUpdate) cWarn else cTextDim)
                textSize = 11f
                gravity = Gravity.CENTER
                setPadding(0, px(4), 0, 0)
            })
        }
    }

    // ─────────────────────────────────────────────────────────────────────────  actions

    private fun onAddStudentClicked() {
        val cls = currentClass ?: return
        startActivity(Intent(this, MainActivity::class.java).apply {
            putExtra(MainActivity.EXTRA_TARGET_CLASS, cls)
        })
    }

    private fun onBulkAddClicked() {
        val cls = currentClass ?: return
        startActivity(Intent(this, MainActivity::class.java).apply {
            putExtra(MainActivity.EXTRA_TARGET_CLASS, cls)
            putExtra(MainActivity.EXTRA_MODE, MainActivity.MODE_BULK_PICK)
        })
    }

    /** Học sinh đã có ảnh/embedding thật (enroll qua camera trước đó — kể cả từ lúc app còn
     * chưa có "Quản lý lớp") nhưng chưa gán lớp nào thì không hiện ở bất kỳ lớp nào cả, dù đã
     * "có trong db" (enrolled.json). Nút này cho giáo viên chọn thẳng từ danh sách đó, gán vào
     * lớp đang xem — không phải enroll lại, không đụng ảnh/embedding đã có sẵn. */
    private fun onAssignExistingClicked() {
        val cls = currentClass ?: return
        val unassigned = attendanceStore.realEnrolledNames()
            .filter { attendanceStore.classNameOf(it) == null }
        if (unassigned.isEmpty()) {
            android.widget.Toast.makeText(this, "Không có học sinh nào đã enroll mà chưa gán lớp.", android.widget.Toast.LENGTH_SHORT).show()
            return
        }
        val labels = unassigned.map { n ->
            val alias = attendanceStore.aliasOf(n)
            val samples = attendanceStore.samplesOf(n).size
            val nameLabel = if (alias != n) "$alias ($n)" else n
            "$nameLabel — $samples ảnh"
        }.toTypedArray()
        val checked = BooleanArray(unassigned.size)
        AlertDialog.Builder(this)
            .setTitle("Thêm học sinh đã có sẵn vào \"$cls\"")
            .setMultiChoiceItems(labels, checked) { _, which, isChecked -> checked[which] = isChecked }
            .setPositiveButton("Gán vào lớp") { _, _ ->
                var count = 0
                unassigned.forEachIndexed { i, name -> if (checked[i]) { attendanceStore.setClassName(name, cls); count++ } }
                if (count > 0) android.widget.Toast.makeText(this, "Đã thêm $count học sinh vào \"$cls\"", android.widget.Toast.LENGTH_SHORT).show()
                refreshAll()
            }
            .setNegativeButton("Hủy", null)
            .show()
    }

    private fun openStudent(name: String) {
        startActivity(Intent(this, MainActivity::class.java).apply {
            putExtra(MainActivity.EXTRA_MODE, MainActivity.MODE_VIEW_STUDENT)
            putExtra(MainActivity.EXTRA_STUDENT_NAME, name)
            currentClass?.let { putExtra(MainActivity.EXTRA_TARGET_CLASS, it) }
        })
    }

    private fun promptAddClass() {
        val input = EditText(this).apply { hint = "Tên lớp, ví dụ: Lớp Lá" }
        AlertDialog.Builder(this)
            .setTitle("Thêm lớp mới")
            .setView(wrapDialogInput(input))
            .setPositiveButton("Tạo") { _, _ ->
                val name = input.text.toString().trim()
                if (name.isNotEmpty()) {
                    attendanceStore.addClass(name)
                    currentClass = name
                    refreshAll()
                }
            }
            .setNegativeButton("Hủy", null)
            .show()
    }

    private fun promptRenameClass(oldName: String) {
        val input = EditText(this).apply { setText(oldName); setSelection(oldName.length) }
        AlertDialog.Builder(this)
            .setTitle("Đổi tên lớp")
            .setView(wrapDialogInput(input))
            .setPositiveButton("Lưu") { _, _ ->
                val newName = input.text.toString().trim()
                if (newName.isNotEmpty() && newName != oldName) {
                    attendanceStore.renameClass(oldName, newName)
                    if (currentClass == oldName) currentClass = newName
                    refreshAll()
                }
            }
            .setNegativeButton("Hủy", null)
            .show()
    }

    private fun wrapDialogInput(input: EditText): View {
        val pad = px(20)
        return FrameLayout(this).apply {
            setPadding(pad, px(8), pad, 0)
            addView(input)
        }
    }
}
