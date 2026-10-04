"""In chữ ký NỘI DUNG của .aar (crc từng file, đệ quy vào .jar) — không phụ thuộc timestamp. Dùng: python Tools/aar_sig.py x.aar"""
import sys, zipfile, io, hashlib

def walk(z, pre, out):
    for i in z.infolist():
        if i.is_dir():
            continue
        out.append(pre + i.filename + ":" + format(i.CRC, "08x"))
        if i.filename.endswith(".jar"):
            walk(zipfile.ZipFile(io.BytesIO(z.read(i))), pre + i.filename + "/", out)

out = []
walk(zipfile.ZipFile(sys.argv[1]), "", out)
print(hashlib.sha1("\n".join(sorted(out)).encode()).hexdigest())
