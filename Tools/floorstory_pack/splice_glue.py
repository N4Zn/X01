"""Một lần (có thể chạy lại): thay khối glue fsd trong game_builder_v2.html (từ 'function fsdPlay(){' đến trước 'let fsdPending = null;') bằng glue_v3.js."""
import os
here = os.path.dirname(os.path.abspath(__file__))
html = os.path.join(here, '..', '..', 'WebTools', 'GenericGameBuilder', 'game_builder_v2.html')
s = open(html, encoding='utf-8').read()
a, b = 'function fsdPlay(){', 'let fsdPending = null;'
assert s.count(a) == 1 and s.count(b) == 1, (s.count(a), s.count(b))
i, j = s.index(a), s.index(b)
assert i < j
new = open(os.path.join(here, 'glue_v3.js'), encoding='utf-8').read()
open(html, 'w', encoding='utf-8', newline='').write(s[:i] + new + s[j:])
print('ok', j - i, '->', len(new))
