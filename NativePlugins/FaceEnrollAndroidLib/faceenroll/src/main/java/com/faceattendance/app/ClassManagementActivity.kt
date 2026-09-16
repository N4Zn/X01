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
import android.widget.Space
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity

/**
 * "Quản lý lớp" — entry point mới, thay MainActivity (màn camera) làm icon launcher chính của
 * FA. 2 màn hình (danh sách lớp / danh sách học sinh trong 1 lớp), chuyển bằng cách build lại
 * nội dung của `scroll` — không dùng Fragment/RecyclerView, theo đúng phong cách build-View-
 * bằng-code đã có sẵn trong MainActivity.kt (showManageStudentsDialog/showStudentSamplesDialog).
 *
 * Các thao tác thật (chụp camera, chọn ảnh từ thư viện, xem/xoá từng mẫu ảnh) đều giao lại cho
 * MainActivity qua Intent tường minh — không viết lại pipeline nhận diện/enroll ở đây.
 */
class ClassManagementActivity : AppCompatActivity() {

    private lateinit var attendanceStore: AttendanceStore

    private lateinit var backBtn: TextView
    private lateinit var titleText: TextView
    private lateinit var renameBtn: TextView
    private lateinit var addClassBtn: TextView
    private lateinit var actionBar: LinearLayout
    private lateinit var scroll: ScrollView

    /** null = đang ở màn "Danh sách lớp"; khác null = đang ở màn danh sách học sinh của lớp đó. */
    private var currentClass: String? = null

    private val dp get() = resources.displayMetrics.density
    private fun px(v: Int) = (v * dp).toInt()

    private val palette = listOf(
        0xFF5B3F9E.toInt(), 0xFF1CA184.toInt(), 0xFFC98A1F.toInt(), 0xFFD9577B.toInt(),
        0xFF3E7FD1.toInt(), 0xFF3D9A56.toInt(), 0xFFB15FD1.toInt(), 0xFFC4552B.toInt()
    )
    private fun colorFor(name: String): Int {
        var h = 0
        for (c in name) h = (h * 31 + c.code) % palette.size
        if (h < 0) h += palette.size
        return palette[h]
    }
    private fun initialOf(name: String): String =
        name.trim().firstOrNull()?.uppercaseChar()?.toString() ?: "?"

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        attendanceStore = AttendanceStore(this)
        setContentView(buildRoot())
        showClassList()
    }

    override fun onResume() {
        super.onResume()
        // Quay lại từ MainActivity (vừa enroll/xoá/đổi tên...) — đọc lại dữ liệu từ đĩa và vẽ
        // lại đúng màn đang đứng.
        attendanceStore = AttendanceStore(this)
        if (currentClass == null) showClassList() else showRoster(currentClass!!)
    }

    @Suppress("DEPRECATION", "OVERRIDE_DEPRECATION")
    override fun onBackPressed() {
        if (currentClass != null) showClassList() else super.onBackPressed()
    }

    // ─────────────────────────────────────────────────────────────────────────  layout

    private fun buildRoot(): View {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(Color.parseColor("#121212"))
        }

        val header = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(px(20), px(18), px(20), px(14))
        }
        backBtn = TextView(this).apply {
            text = "←"
            textSize = 22f
            setTextColor(Color.WHITE)
            setPadding(px(4), px(4), px(20), px(4))
            visibility = View.GONE
            setOnClickListener { showClassList() }
        }
        header.addView(backBtn)
        titleText = TextView(this).apply {
            text = "Danh sách lớp"
            textSize = 22f
            setTypeface(typeface, Typeface.BOLD)
            setTextColor(Color.WHITE)
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        header.addView(titleText)
        renameBtn = pillButton("✏ Đổi tên") { currentClass?.let { promptRenameClass(it) } }
        renameBtn.visibility = View.GONE
        header.addView(renameBtn)
        addClassBtn = pillButton("+ Thêm lớp") { promptAddClass() }
        header.addView(addClassBtn)
        root.addView(header)

        actionBar = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            setPadding(px(20), 0, px(20), px(14))
            visibility = View.GONE
        }
        actionBar.addView(actionButton("+ Học sinh mới") { onAddStudentClicked() })
        actionBar.addView(actionButton("Thêm từ camera") { onAddStudentClicked() })
        actionBar.addView(actionButton("Thêm hàng loạt") { onBulkAddClicked() })
        root.addView(actionBar)

        scroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f)
        }
        root.addView(scroll)
        return root
    }

    private fun pillButton(label: String, onClick: () -> Unit): TextView = TextView(this).apply {
        text = label
        setTextColor(Color.parseColor("#90CAF9"))
        textSize = 14f
        setPadding(px(12), px(8), px(12), px(8))
        setOnClickListener { onClick() }
    }

    private fun actionButton(label: String, onClick: () -> Unit): Button = Button(this).apply {
        text = label
        isAllCaps = false
        layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).also {
            it.marginEnd = px(8)
        }
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
                if (i < items.size) { cell.addView(items[i]); i++ }
                row.addView(cell)
            }
            container.addView(row)
        }
        return container
    }

    private fun emptyState(msg: String): View = TextView(this).apply {
        text = msg
        setTextColor(Color.parseColor("#888888"))
        textSize = 14f
        gravity = Gravity.CENTER
        setPadding(px(40), px(60), px(40), px(60))
    }

    private fun card(onClick: () -> Unit): LinearLayout = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(px(14), px(14), px(14), px(14))
        background = GradientDrawable().apply {
            setColor(Color.parseColor("#1E1E1E"))
            cornerRadius = px(14).toFloat()
        }
        isClickable = true
        isFocusable = true
        setOnClickListener { onClick() }
    }

    /** Circular avatar: the student's representative sample photo if one exists, otherwise a
     * colored circle with their name's initial (same "no photo yet" look the roster/photo grids
     * use to flag someone as needing a picture). */
    private fun avatarWithInitial(name: String, sizeDp: Int, photoPath: String?): View {
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
                setColor(if (bmp != null) Color.parseColor("#1E1E1E") else colorFor(name))
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

    // ─────────────────────────────────────────────────────────────────────────  screens

    private fun showClassList() {
        currentClass = null
        titleText.text = "Danh sách lớp"
        backBtn.visibility = View.GONE
        renameBtn.visibility = View.GONE
        addClassBtn.visibility = View.VISIBLE
        actionBar.visibility = View.GONE

        val classes = attendanceStore.allClassNames()
        scroll.removeAllViews()
        scroll.addView(
            if (classes.isEmpty()) {
                emptyState("Chưa có lớp nào.\nBấm \"+ Thêm lớp\" ở trên để tạo lớp đầu tiên.")
            } else {
                buildGrid(classes.mapIndexed { i, c -> classCardView(c, i) }, 3)
            }
        )
    }

    private fun classCardView(className: String, index: Int): View {
        val students = attendanceStore.studentsInClass(className)
        val needing = students.count { attendanceStore.samplesOf(it).isEmpty() }
        return card { showRoster(className) }.apply {
            addView(TextView(this@ClassManagementActivity).apply {
                text = "${index + 1}. $className"
                setTextColor(Color.WHITE)
                textSize = 16f
                setTypeface(typeface, Typeface.BOLD)
            })
            addView(TextView(this@ClassManagementActivity).apply {
                text = "${students.size} học sinh" + if (needing > 0) " · $needing cần ảnh" else ""
                setTextColor(if (needing > 0) Color.parseColor("#FFB74D") else Color.parseColor("#AAAAAA"))
                textSize = 12f
                setPadding(0, px(4), 0, 0)
            })
        }
    }

    private fun showRoster(className: String) {
        currentClass = className
        titleText.text = className
        backBtn.visibility = View.VISIBLE
        renameBtn.visibility = View.VISIBLE
        addClassBtn.visibility = View.GONE
        actionBar.visibility = View.VISIBLE

        val students = attendanceStore.studentsInClass(className)
        scroll.removeAllViews()
        scroll.addView(
            if (students.isEmpty()) {
                emptyState("Lớp \"$className\" chưa có học sinh nào.\nDùng các nút bên trên để thêm.")
            } else {
                buildGrid(students.mapIndexed { i, s -> studentCardView(s, i) }, 3)
            }
        )
    }

    private fun studentCardView(name: String, index: Int): View {
        val samples = attendanceStore.samplesOf(name)
        val needsUpdate = samples.isEmpty()
        return card { openStudent(name) }.apply {
            gravity = Gravity.CENTER_HORIZONTAL
            addView(TextView(this@ClassManagementActivity).apply {
                text = "${index + 1}"
                setTextColor(Color.parseColor("#777777"))
                textSize = 10f
            })
            addView(avatarWithInitial(name, 60, attendanceStore.representativePhoto(name)).also {
                (it.layoutParams as? ViewGroup.MarginLayoutParams)?.topMargin = px(4)
                it.setPadding(0, px(4), 0, px(8))
            })
            addView(TextView(this@ClassManagementActivity).apply {
                text = name
                setTextColor(Color.WHITE)
                textSize = 14f
                setTypeface(typeface, Typeface.BOLD)
                gravity = Gravity.CENTER
                maxLines = 1
            })
            addView(TextView(this@ClassManagementActivity).apply {
                text = if (needsUpdate) "Cần cập nhật ảnh" else "${samples.size} mẫu ảnh"
                setTextColor(if (needsUpdate) Color.parseColor("#FFB74D") else Color.parseColor("#AAAAAA"))
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
                    showClassList()
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
                    if (currentClass == oldName) showRoster(newName) else showClassList()
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
