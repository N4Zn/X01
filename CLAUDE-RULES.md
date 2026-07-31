# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 1. Project Overview

**Project Name**: vibe-code
**Description**: Bộ rule và framework hỗ trợ Claude Code trong việc research, troubleshoot, decision making và documentation.

---

## 2. Quick Start

### Activation Keywords

| Keyword | Action |
|---------|--------|
| `RCA` / `root cause` | Basic RCA (5 Whys) |
| `deep RCA` | Deep-dive postmortem |
| `define docs task status` | Tạo task tracking document |
| `define doc naming` | Áp dụng naming convention |
| `advance ascii diagram` | Dùng ASCII diagram templates |
| `updated task status [component] tracker` | Cập nhật task tracker |
| `Visual information` | UC1: Trực quan hóa thông tin |
| `Overview research` | UC2: Research architecture/công nghệ |
| `Detail research` | UC3: Phân tích sâu, định lượng |
| `Troubleshoot rule` | UC4: Debug, tìm root cause |
| `Decision making` | UC5: Ra quyết định với trade-offs |

### Session End Checklist

Luôn cung cấp khi kết thúc session:
1. What was changed?
2. Files modified (name + full path)
3. Approach applied
4. Final report & outcomes

---

## 3. Danh Sách Rules

### 3.1 Nhóm Decomposition (Phân rã vấn đề)

#### MECE (Mutually Exclusive, Collectively Exhaustive)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Phân loại không overlap, bao phủ tất cả cases. Đảm bảo không bỏ sót, không trùng lặp |
| **Cách dùng** | Chia vấn đề thành các nhóm loại trừ lẫn nhau, kiểm tra đã bao phủ hết chưa |
| **Use Cases** | **Primary**: UC1 (Visual Information) |

#### First Principles (Tư duy từ gốc)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Phá vỡ assumptions, quay về fundamentals, rebuild giải pháp từ đầu |
| **Cách dùng** | Step 1: Challenge Assumptions → Step 2: Identify Fundamentals → Step 3: Rebuild from Scratch |
| **Use Cases** | **Primary**: UC2 (Overview Research) |

#### Fermi Technique (Ước lượng định lượng)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Định lượng bằng formula, ước lượng con số khi không có data chính xác |
| **Cách dùng** | Question → Break into Components → Estimate Each → Formula → Sanity Check → Confidence Range |
| **Use Cases** | **Primary**: UC3 (Detail Research) |

---

### 3.2 Nhóm RCA (Root Cause Analysis)

#### 5 Whys (Hỏi 5 lần Tại sao)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Drill down liên tục để tìm TRUE root cause, không dừng ở symptom |
| **Cách dùng** | Why? → Why? → Why? → Why? → Why? = ROOT CAUSE |
| **Use Cases** | **Primary**: UC4 (Troubleshoot) |

#### Fishbone / Ishikawa (Xương cá)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Phân loại nguyên nhân theo categories: Methods, Materials, Machines, People, Environment |
| **Cách dùng** | Vẽ diagram xương cá, mỗi nhánh là 1 category, liệt kê causes vào từng nhánh |
| **Use Cases** | **Primary**: UC4 (Troubleshoot) |

#### Fault Tree (Cây lỗi)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Phân tích lỗi bằng logic diagram với AND/OR gates |
| **Cách dùng** | Từ top event (lỗi), vẽ cây logic AND/OR xuống các contributing events |
| **Use Cases** | **Primary**: UC4 (Troubleshoot) |

#### Causal Chain (Chuỗi nhân quả)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Liên kết các events rời rạc thành chuỗi: Trigger → Amplifier → Symptom |
| **Cách dùng** | Xác định trigger gốc, yếu tố khuếch đại, và triệu chứng quan sát được |
| **Use Cases** | **Supporting**: UC4 (Troubleshoot) |

---

### 3.3 Nhóm Decision (Ra quyết định)

#### Occam's Razor (Dao cạo Occam)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Khi có nhiều solutions tương đương, chọn giải pháp đơn giản nhất |
| **Cách dùng** | So sánh các options, loại bỏ complexity không cần thiết |
| **Use Cases** | **Primary**: UC5 (Decision Making) |

#### Type 1 / Type 2 Decisions

| Field | Detail |
|-------|--------|
| **Tác dụng** | Phân loại quyết định: Type 1 (không thể đảo ngược) = slow down; Type 2 (đảo ngược được) = move fast |
| **Cách dùng** | Xác định quyết định thuộc loại nào → điều chỉnh tốc độ và mức đầu tư phân tích |
| **Use Cases** | **Primary**: UC5 (Decision Making) |

#### Second-Order Thinking (Tư duy bậc 2)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Xét hậu quả dài hạn: "And then what?" — consequences bậc 2, bậc 3 |
| **Cách dùng** | Với mỗi quyết định, hỏi liên tục "rồi sao nữa?" để thấy cascading effects |
| **Use Cases** | **Primary**: UC5 (Decision Making) · **Supporting**: UC2 (Overview Research) |

#### RICE / MoSCoW (Scoring ưu tiên)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Prioritization có scoring: RICE = (Reach × Impact × Confidence) / Effort. MoSCoW = Must/Should/Could/Won't |
| **Cách dùng** | Gán điểm cho từng option theo công thức, xếp hạng theo tổng điểm |
| **Use Cases** | **Primary**: UC3 (Detail Research) · **Supporting**: UC5 (Decision Making) |

---

### 3.4 Nhóm Communication (Truyền đạt)

#### Pyramid Principle (Nguyên tắc Kim tự tháp)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Kết luận trước, chi tiết sau. Người đọc nắm được key message ngay lập tức |
| **Cách dùng** | Mở đầu bằng conclusion/recommendation → supporting arguments → details |
| **Use Cases** | **Primary**: UC1 (Visual Information) |

#### SCQA (Situation-Complication-Question-Answer)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Cấu trúc truyền đạt rõ ràng: Tình huống → Vấn đề → Câu hỏi → Giải đáp |
| **Cách dùng** | Viết theo 4 phần: S (bối cảnh) → C (vấn đề phát sinh) → Q (câu hỏi cần trả lời) → A (giải pháp) |
| **Use Cases** | **Supporting**: UC3 (Detail Research) |

#### Feynman Technique (Kỹ thuật Feynman)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Đơn giản hóa để dễ hiểu — giải thích như cho trẻ 12 tuổi, tìm gaps trong hiểu biết |
| **Cách dùng** | Giải thích concept bằng ngôn ngữ đơn giản → tìm chỗ không giải thích được → học lại → simplify |
| **Use Cases** | **Supporting**: UC1 (Visual Information) |

---

### 3.5 Nhóm Thinking (Tư duy phân tích)

#### Dragonfly-Eye View (Nhìn đa chiều)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Phân tích 5 chiều: Technical, Resources, Risks, System Requirements, Business |
| **Cách dùng** | Đánh giá mỗi option/vấn đề qua cả 5 dimensions, không chỉ technical |
| **Use Cases** | **Supporting**: UC2 (Overview Research) · UC3 (Detail Research) · UC5 (Decision Making) |

#### Systems Thinking (Tư duy hệ thống)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Tìm feedback loops, leverage points, hidden connections, unintended consequences |
| **Cách dùng** | Vẽ system map, xác định feedback loops (reinforcing/balancing), tìm leverage points |
| **Use Cases** | **Supporting**: UC2 (Overview Research) · UC4 (Troubleshoot) · UC5 (Decision Making) |

#### Socratic Method (Phương pháp Socrates)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Deep questioning để probe assumptions và evidence: "What do you mean?", "What evidence?" |
| **Cách dùng** | Hỏi clarifying questions → probe assumptions → probe evidence → explore implications |
| **Use Cases** | **Supporting**: UC3 (Detail Research) |

#### Conway's Law

| Field | Detail |
|-------|--------|
| **Tác dụng** | Architecture phản ánh cấu trúc tổ chức — muốn đổi architecture thì đổi team structure trước |
| **Cách dùng** | Khi thiết kế system, xem xét org structure hiện tại và mong muốn |
| **Use Cases** | **Supporting**: UC2 (Overview Research) |

#### Inversion / Pre-Mortem (Tư duy ngược)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Hỏi "làm sao để thất bại?" để phòng ngừa — tìm risks bằng cách nghĩ ngược |
| **Cách dùng** | Giả sử project đã thất bại → liệt kê lý do → tạo prevention plan |
| **Use Cases** | **Supporting**: UC4 (Troubleshoot) |

#### Pareto 80/20

| Field | Detail |
|-------|--------|
| **Tác dụng** | 80% kết quả đến từ 20% nguyên nhân — focus vào vital few |
| **Cách dùng** | Xác định top 20% causes/tasks tạo ra 80% impact → ưu tiên chúng |
| **Use Cases** | **Supporting**: UC4 (Troubleshoot) |

#### OODA Loop

| Field | Detail |
|-------|--------|
| **Tác dụng** | Rapid response cycle: Observe → Orient → Decide → Act → Loop lại |
| **Cách dùng** | Dùng khi cần phản ứng nhanh trong incident — quan sát, định hướng, quyết định, hành động |
| **Use Cases** | **Supporting**: UC4 (Troubleshoot) |

#### Seek New Tools

| Field | Detail |
|-------|--------|
| **Tác dụng** | Tìm kiếm công nghệ/approaches mới ngoài những gì đã biết, tránh lối mòn |
| **Cách dùng** | Research alternatives, benchmark mới, evaluate emerging technologies |
| **Use Cases** | **Primary**: UC2 (Overview Research) |

#### Practical Thinking (Tư duy thực tiễn)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Feasibility check: đánh giá Resources, Constraints, Assumptions trước khi implement |
| **Cách dùng** | Kiểm tra: có đủ resources? constraints là gì? assumptions nào cần validate? |
| **Use Cases** | **Primary**: UC3 (Detail Research) |

#### Continuous Improvement (Cải tiến liên tục)

| Field | Detail |
|-------|--------|
| **Tác dụng** | Gather feedback, iterate, apply 5 Whys vào chính analysis process |
| **Cách dùng** | Sau mỗi iteration: review kết quả → tìm gaps → cải tiến approach |
| **Use Cases** | **Supporting**: UC3 (Detail Research) |

#### Timeline/Sequence

| Field | Detail |
|-------|--------|
| **Tác dụng** | Sắp xếp theo thứ tự thời gian, thể hiện cause-effect sequence |
| **Cách dùng** | Vẽ timeline chronological, map events theo thời gian |
| **Use Cases** | **Supporting**: UC1 (Visual Information) |

---

## 4. Use Cases (Trường hợp sử dụng)

### 4.1 UC1: Visual Information

| Field | Detail |
|-------|--------|
| **Keyword** | `Visual information` |
| **Mục đích** | Tổ chức thông tin trực quan, dễ hiểu. Sắp xếp có thứ tự, nhân quả, logic flow |
| **Primary Rules** | MECE, Pyramid Principle |
| **Supporting Rules** | Feynman Technique, Timeline/Sequence |

**Process**: Identify Information Type → Apply MECE Check → Apply Pyramid → Simplify (Feynman)

---

### 4.2 UC2: Overview Research

| Field | Detail |
|-------|--------|
| **Keyword** | `Overview research` |
| **Mục đích** | Research architecture, best practice, công nghệ mới. Think outside the box |
| **Primary Rules** | First Principles, Seek New Tools |
| **Supporting Rules** | Dragonfly-Eye View, Systems Thinking, Conway's Law, Second-Order Thinking |

**Process**: Challenge Assumptions → Identify Fundamentals → Rebuild from Scratch

---

### 4.3 UC3: Detail Research

| Field | Detail |
|-------|--------|
| **Keyword** | `Detail research` |
| **Mục đích** | Phân tích sâu, định lượng, ưu tiên có thứ tự. Phân biệt hypothesis vs evidence |
| **Primary Rules** | Fermi Technique, RICE/MoSCoW, Practical Thinking |
| **Supporting Rules** | Dragonfly-Eye View, Socratic Method, SCQA, Continuous Improvement |

**Process**: Question → Break into Components → Estimate → Formula → Sanity Check → Confidence Range

---

### 4.4 UC4: Troubleshoot

| Field | Detail |
|-------|--------|
| **Keyword** | `Troubleshoot rule` |
| **Mục đích** | Debug, RCA, tìm root cause. Link các issues rời rạc thành journey |
| **Primary Rules** | 5 Whys, Fishbone, Fault Tree |
| **Supporting Rules** | OODA Loop, Systems Thinking, Causal Chain, Inversion, Pareto |

**Process**: Incident Summary → Timeline → 5 Whys → Fishbone → Causal Chain → Actions

---

### 4.5 UC5: Decision Making

| Field | Detail |
|-------|--------|
| **Keyword** | `Decision making` |
| **Mục đích** | Ra quyết định hiệu quả với trade-offs và uncertainty. Chọn giải pháp đơn giản nhất |
| **Primary Rules** | Occam's Razor, Type 1/Type 2, Second-Order Thinking |
| **Supporting Rules** | RICE/MoSCoW, Dragonfly-Eye View, Systems Thinking |

**Process**: Classify (Type 1/2?) → Evaluate (Dragonfly-Eye + RICE) → Occam's Razor → Second-Order → Recommendation

---

## 5. Operational Rules (Quy tắc vận hành)

### 5.1 Folder Structure

**Activation**: Luôn active cho mọi file operations.

- Ask permission before removing any file/folder
- Documentation → `docs/` · Scripts → `scripts/` · Logs → `logs/` · Journey → `overview_projects/` · K8s manifests → `manifest/`

```
<component-name>/
├── manifest/           # Kubernetes manifests (*.yaml)
├── scripts/            # Bash scripts
│   └── lib/            # Shared functions (common.sh)
├── docs/               # Documentation (SPEC-*, RCA-*, GUIDE-*)
└── logs/               # Collected logs (logs-YYYYMMDD-HHMMSS/)
```

---

### 5.2 Basic RCA

**Activation**: Khi user hỏi `RCA`, `root cause`, `investigate` (không có "deep").
**Persona**: Technical Analyst

- Dùng 5 Whys method
- Output: Problem Statement → Evidence → 5 Whys → Solution Options (table) → Recommendation

---

### 5.3 Deep RCA

**Activation**: Khi user nói `deep RCA`.
**Persona**: Senior SRE / Distributed-Systems Engineer

**Analysis Requirements**:
- 5 Whys: tìm TRUE root cause
- Separation of Concerns: Control Plane vs Data Plane, Trigger vs Amplifier vs Symptom
- Correlation Analysis: App logs, Infra metrics, Network/DNS, Latency timelines
- Hypothesis Testing: validate/falsify alternatives
- Mark confidence: `[HIGH]`, `[MEDIUM]`, `[LOW]`

**Mandatory Postmortem Structure**:
```
1. Problem Statement (symptoms + metrics)
2. Impact Assessment (scope, duration, business impact)
3. Timeline of Events (timestamps, correlation)
4. 5 Whys Analysis (Why1→Why2→Why3→Why4→Why5=ROOT CAUSE)
5. Root Cause (definitive statement + evidence)
6. Contributing Factors
7. Corrective Actions (short-term, owner + ETA)
8. Preventive Actions (long-term, architecture/monitoring/process)
9. Lessons Learned
```

**Causal Chain Diagram**:
```
+------------------+     +---------------------+     +-------------------+
|  TRIGGER         |────>|  AMPLIFIER          |────>|  SYMPTOM          |
+------------------+     +---------------------+     +-------------------+
| [Root trigger    |     | [What amplified     |     | [Observable       |
|  event]          |     |  the problem]       |     |  symptoms]        |
+------------------+     +---------------------+     +-------------------+

CONTROL PLANE ═══════════════════════════════════════════════════════════
DATA PLANE ──────────────────────────────────────────────────────────────
```

---

### 5.4 Task Track

**Activation**: Khi user nói `updated task status [component] tracker`.

**Timestamp Rule**: Chỉ dùng CURRENT time (past→now), format `YYYY-MM-DD HH:MM (UTC+7)`, NEVER future timestamps.

**Self-Validation**:
1. `TZ='Asia/Ho_Chi_Minh' date '+%Y-%m-%d %H:%M'`
2. Validate: timestamp ≤ current time
3. Verify file modification times: `stat --format='%y %n' /path/to/file`
4. Map timeline entries to actual file modification timestamps

**Timeline Format**: `| YYYY-MM-DD | HH:MM | Activity | Result | Status |`

**Tracker Template**:
```markdown
#### [Component] Tracker
| Field | Value |
|-------|-------|
| **Keyword** | `updated task status [component] tracker` |
| Status file | `[path]/docs/task-[component]-status.md` |
```

---

### 5.5 Doc Naming Convention

**Activation**: Khi user nói `define doc naming` hoặc khi tạo documentation files mới.

**Format**: `[TYPE]-v[VERSION]-[SCOPE]-[KEYWORD].md`

| Type | Purpose | Example |
|------|---------|---------|
| `RCA` | Investigation & findings | RCA-v1-kafka-latency.md |
| `TASK` | Progress tracking | TASK-v1-kafka-latency-optimize.md |
| `WARN` | Risk documentation | WARN-v1-kafka-solution-downsides.md |
| `PLAN` | Strategy & steps | PLAN-v1-kafka-kraft-migration.md |
| `SPEC` | Technical requirements | SPEC-v1-kafka-cluster-design.md |
| `GUIDE` | How-to documentation | GUIDE-v1-k8s-aliases.md |
| `REF` | Quick lookup | REF-v1-kafka-config-params.md |

**Version Rules**: MAJOR = new direction/scope change · MINOR = refinement/validation of same investigation

---

### 5.6 Define Docs Task Status

**Activation**: Khi user nói `define docs task status`.
**Persona**: Project Manager / Technical Lead

**Document Structure**:
```markdown
# Task List Status: [Project Name]
**Created**: [Date] · **Last Updated**: [Date] · **Status**: [Icon + Summary] · **Owner**: [Team]

## Quick Status Overview (ASCII box with phase summaries)
## Timeline (| Date | Time | Activity | Result | Status |)
## Solution Overview (Impact/Effort matrix + summary table)
## Phase N: [Phase Name] (Baseline → Solutions → Validation)
## Document References · Status Icons Legend · Journey Log · Notes
```

**Phase Convention**: P0 (Emergency) · P0.5 (Investigation) · P1 (Optimization) · P2 (Optional) · Validation (Testing)
**Task ID Format**: `P[Phase]-S[Solution#]` hoặc `P[Phase]-T[Task#]`
**Status Icons**: ✅ Done · ⚠️ Warning · ❌ Failed · ⏭️ Skipped · ⬜ Pending · 🔄 In progress

---

## 6. Output Standards

### Document Structure (bắt buộc)

1. **Header**: Document name, dates, status
2. **Key Findings**: Bảng tóm tắt ngay sau Header, trước Mục lục (Pyramid — conclusion first)
3. **Body**: MECE structure (no overlap, all covered)
4. **Evidence**: Data, logs, metrics, diagrams
5. **References**: Related documents

### Key Findings Format (bắt buộc cho RESEARCH / GUIDE / SPEC)

```markdown
## Key Findings

| # | Finding | Chi tiết | Confidence |
|---|---------|----------|------------|
| 1 | **[Tên finding]** | [Mô tả 1 dòng] | [HIGH/MEDIUM/LOW] |
```

**Rules**: Đặt ngay sau Header · 5-8 findings · Bold keyword chính · Chi tiết ngắn gọn 1 dòng

### ASCII Diagrams

**Activation**: `advance ascii diagram` hoặc khi cần diagram phức tạp.
**Full Templates**: See [REF-v2-ascii-diagram-templates.md](rule-research/REF-v2-ascii-diagram-templates.md)

| Câu hỏi | Diagram Type |
|----------|--------------|
| "What components exist?" | Architecture / Container |
| "How do services communicate?" | Sequence |
| "What happens when X occurs?" | State Machine / Activity |
| "Why did this fail?" | Causal Chain / Decision Tree |
| "How does the process flow?" | Flowchart / Activity |
| "What's the timeline?" | Timeline / Gantt |
| "Where are the bottlenecks?" | Heat Map / Bottleneck |

### Session Summary (cuối mỗi session)

```
1. What was changed?
2. Files modified (name + full path)
3. Approach applied
4. Final report & outcomes
```

---

## 7. Reference Documents

| Document | Purpose |
|----------|---------|
| [RULE-v1-core-thinking-use-cases.md](rule-research/RULE-v1-core-thinking-use-cases.md) | Full Use Case details, Output Standards, Process Templates |
| [REF-v1-analytical-frameworks-technical-thinking(For-claude).md](rule-research/REF-v1-analytical-frameworks-technical-thinking(For-claude).md) | Detailed thinking frameworks (MECE, Feynman, OODA, etc.) |
| [REF-v2-ascii-diagram-templates.md](rule-research/REF-v2-ascii-diagram-templates.md) | ASCII diagram templates (20+ types) |
