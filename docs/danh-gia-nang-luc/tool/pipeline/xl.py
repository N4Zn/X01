import json
from openpyxl import Workbook
from openpyxl.styles import Font,PatternFill,Alignment,Border,Side
out=json.load(open('out.json',encoding='utf8'))
HP=['Nhận biết số','Đếm','Cộng','Trừ','So sánh']
CALL={'Ngô Quốc An':'Quốc An','Đặng Minh Anh':'Ỉn','Nguyễn Đăng Bách':'Đăng Bách','Nguyễn Ngọc Bảo Châu':'Bảo Châu','Vũ Đình Khánh':'Đình Khánh','Nguyễn Anh Khôi':'Dino','Tạ Ngọc Khuê':'Ngọc Khuê','Nguyễn Phương Linh':'Phương Linh','Đào Khánh Ngọc':'É','Phạm Minh Ngọc':'Minh Ngọc','Đặng Tâm Như':'Tâm Như','Nguyễn Hà Linh Phương':'Linh Phương','Dư Thanh Trà':'Bông','Doãn Minh Trí':'Minh Trí','Đinh Nguyễn Cát Tường':'Cát Tường','Nguyễn Hòa Vũ':'Hòa Vũ','Trần Bảo Vy':'Em Bé','Trương Thảo Vy':'Thảo Vy'}
def lv(p): return 'Đạt mục tiêu' if p>=75 else 'Mức 3' if p>=50 else 'Mức 2' if p>=25 else 'Mức 1'
wb=Workbook(); ws=wb.active; ws.title='Tổng hợp'
hdr=['ID','Tên đầy đủ','Tên thường gọi']+HP+['Kết quả tổng (Toán)','Mức','Số câu tính điểm','Ghi chú']
ws.append(['KẾT QUẢ ĐÁNH GIÁ TOÁN – log ngày 08/10/2026 – Điểm học phần = 80 × tỉ lệ đúng × hệ số tốc độ (1,25 nếu TB ≤5s; 1,0 nếu ≥20s; tuyến tính ở giữa; tối đa 100). Tổng = TB các học phần có chơi. Ô vàng = học phần ít mẫu (<10 câu). Chỉ gồm các bé đã chơi và có điểm; dữ liệu log 08/10/2026 (chưa gồm log cũ).'])
ws.append(hdr)
thin=Side(style='thin',color='BBBBBB'); yel=PatternFill('solid',fgColor='FFF2CC'); head=PatternFill('solid',fgColor='1F3864')
for d in [o for o in out if 'toan' in o]:
    r=[d['code'],d['name'],CALL[d['name']]]
    for h in HP: r.append(round(d['hp'][h]['pt'],1) if h in d['hp'] else None)
    notes=[]
    if 'toan' in d:
        r+= [round(d['toan'],1),lv(d['toan']),d['n']]
        if d['n']<15: notes.append('ÍT DỮ LIỆU: chỉ %d câu tính điểm'%d['n'])
        low=[h for h in HP if h in d['hp'] and d['hp'][h]['low']]
        if low: notes.append('Học phần ít mẫu (<10 câu): '+', '.join(low))
        miss=[h for h in HP if h not in d['hp']]
        if miss: notes.append('CHƯA CÓ ĐIỂM học phần: '+', '.join(miss))
    else:
        r+=[None,None,0]; notes.append('Chưa có dữ liệu tính điểm (chỉ chơi game thi đấu/phiên bỏ dở)')
    r.append('; '.join(notes)); ws.append(r)
    i=ws.max_row
    for j,h in enumerate(HP):
        if h in d['hp'] and d['hp'][h]['low']: ws.cell(i,4+j).fill=yel
ws.merge_cells(start_row=1,start_column=1,end_row=1,end_column=len(hdr)); ws['A1'].alignment=Alignment(wrap_text=True,vertical='top'); ws.row_dimensions[1].height=48
for c in ws[2]: c.font=Font(bold=True,color='FFFFFF'); c.fill=head; c.alignment=Alignment(horizontal='center',vertical='center',wrap_text=True)
for row in ws.iter_rows(min_row=3):
    for c in row: c.border=Border(top=thin,bottom=thin,left=thin,right=thin)
    row[8].font=Font(bold=True)
for col,w in zip('ABCDEFGHIJKL',[13,24,14,13,9,9,9,10,16,14,12,52]): ws.column_dimensions[col].width=w
ws.freeze_panes='D3'
w2=wb.create_sheet('Chi tiết học phần')
w2.append(['ID','Tên thường gọi','Học phần','Đúng','Số câu','% đúng','TB giây (câu đúng)','Hệ số tốc độ','Điểm','Ít mẫu'])
for d in out:
    for h in HP:
        if h in d['hp']:
            v=d['hp'][h]; w2.append([d['code'],CALL[d['name']],h,v['ok'],v['n'],round(v['acc'],1),round(v['t'],1) if v['t'] else None,round(v['k'],2),round(v['pt'],1),'x' if v['low'] else ''])
for c in w2[1]: c.font=Font(bold=True,color='FFFFFF'); c.fill=head
for col,w in zip('ABCDEFGHIJ',[13,14,14,7,8,8,18,13,8,8]): w2.column_dimensions[col].width=w
w3=wb.create_sheet('Thi đấu & loại trừ')
for r in [['Game thi đấu vui (không tính điểm)'],['ChuCaiThuong 155136','Đào Khánh Ngọc 9/14 đúng (64%) – Doãn Minh Trí 7/13 (54%)'],['ChuCaiThuong 155431','Đào Khánh Ngọc 11/15 (73%) – Doãn Minh Trí 10/16 (62%)'],[],['Loại khỏi tính điểm'],['151551–153818 (AddNumber5Digit, DemQua)','Phiên thử nghiệm (Blue_1/Red_1, bấm bừa, DemQua luôn đúng)'],['2025-04-14_162449_TongHopToan','Phiên thử 15:43 (đồng hồ máy sai ngày)'],['153852_AddNumber5Digit','File 0 byte'],['161149, 161521, 161922, 161928, 163222','Phiên bỏ dở (<5 câu)'],['161209_Counting.json','Trùng 161210 (không có tên)'],['Quy ước','161210_Counting ô phải = Đinh Nguyễn Cát Tường; câu chưa nhận diện gán cho bé của ô; ID xếp alphabet theo tên (chữ cuối) rồi họ đệm, lớp 5 tuổi 17 bé = CASA-21001..21017 (roster K02_backup 06/10); Đặng Minh Anh 4 tuổi (thêm 06–07/10, alias Ỉn) = CASA-22001; tên thường gọi = alias trong roster']]: w3.append(r)
w3['A1'].font=Font(bold=True); w3['A5'].font=Font(bold=True); w3.column_dimensions['A'].width=42; w3.column_dimensions['B'].width=90
w4=wb.create_sheet('Chưa có điểm')
w4.append(['ID','Tên đầy đủ','Tên thường gọi','Ghi chú'])
for d in [o for o in out if 'toan' not in o]:
    w4.append([d['code'],d['name'],CALL[d['name']],'Chưa có điểm: chưa có câu nào ở game tính điểm (Đếm/Cộng/Trừ/So sánh/Nhận biết số) trong log 08/10. Chờ log bổ sung.'])
for c in w4[1]: c.font=Font(bold=True,color='FFFFFF'); c.fill=head
for col,w in zip('ABCD',[13,24,14,90]): w4.column_dimensions[col].width=w
wb.save('C:/Users/ADMIN/Downloads/GameLogs/KetQua_Toan_20261008.xlsx'); print('ok')
