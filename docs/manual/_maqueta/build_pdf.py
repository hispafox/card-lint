#!/usr/bin/env python3
# Genera el PDF del manual de usuario de card-lint a partir de «manual-usuario.md», con la maqueta de la casa.
# Copia del generador del temario ampliado de CLNCOD (Desarrollos/CLNCOD - Clean Code avanzado/entregables/_maqueta/
# build_pdf.py en el vault), que sale del de GHCOPAG, este del de TSTCLN y este del de BLAZOR: formato 150 x 226 mm,
# Poppins + Lora, índigo y ámbar, cornisas y aperturas con número grande, y cada capítulo en página nueva.
# Cambios: el .md, el nombre del PDF, la portada, las rutas (el manual está en esta misma carpeta de arriba) y las
# imágenes, que el temario no lleva: «![pie](capturas/x.png)» en una línea sola sale como figura con su pie.
# Necesita WeasyPrint con el runtime de GTK y las fuentes de la casa (memoria #973 de Engram).
# La fuente de verdad es el .md: si cambia el texto, se vuelve a ejecutar este script.
#
# Uso:  python build_pdf.py [--png DIR]
import os, re, sys, textwrap, html as H
from pathlib import Path

LOCAL = os.environ.get('LOCALAPPDATA', '')
os.environ.setdefault('WEASYPRINT_DLL_DIRECTORIES', LOCAL + r'\gtk3-runtime' + '\\$_63_')
os.environ.setdefault('FONTCONFIG_PATH', LOCAL + r'\gtk3-runtime\etc\fonts')

HERE = Path(__file__).resolve().parent
DEV = HERE.parent
OUT = HERE.parent
FONTS = 'file:///' + LOCAL.replace('\\', '/') + '/Microsoft/Windows/Fonts/'
# Consolas por ruta: sin la configuración de fontconfig, WeasyPrint no siempre la encuentra por nombre.
MONO = 'file:///' + os.environ.get('WINDIR', r'C:\Windows').replace('\\', '/') + '/Fonts/consola.ttf'

DOCS = [
    # Pedro, 25-11-2026: el manual de usuario de card-lint, «con el estilo nuestro de siempre».
    {'md': 'manual-usuario.md', 'pdf': 'manual-usuario.pdf',
     'kicker': 'Manual de usuario', 'eyebrow': 'Qué es card-lint', 'indice': 'Capítulos',
     'meta': 'Primera versión · noviembre de 2026 · con Cppcheck 2.22.0 y PMD 7.29.0', 'saltos': True},
]

# ---------------------------------------------------------------- parseo del .md
def inline(s):
    s = H.escape(s, quote=False)
    s = re.sub(r'\[([^\]]+)\]\((https?://[^)\s]+)\)', r'<a href="\2">\1</a>', s)
    s = re.sub(r'`([^`]+)`', r'<code>\1</code>', s)
    s = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', s)
    s = re.sub(r'(?<![\*\w])\*(?!\s)(.+?)(?<!\s)\*(?![\*\w])', r'<em>\1</em>', s)
    return s

def plain(s):
    """Texto sin marcas de markdown, para la cornisa y el índice."""
    s = re.sub(r'\*\*\[[^\]]*\]\*\*', '', s)
    return re.sub(r'[*`]', '', s).strip()

def strip_frontmatter(md):
    if md.startswith('---'):
        end = md.find('\n---', 3)
        if end != -1:
            return md[end + 4:]
    return md

FENCE = re.compile(r'^(\s*)```')

def sections(md):
    tree, cur = [], {'level': 0, 'title': '', 'lines': []}
    tree.append(cur)
    en_codigo = False
    for ln in md.splitlines():
        if FENCE.match(ln):
            en_codigo = not en_codigo
        # Dentro de un bloque de código, una línea con «#» es código, no un título.
        m = None if en_codigo else re.match(r'^(#{1,3}) (.+)$', ln)
        if m:
            cur = {'level': len(m.group(1)), 'title': m.group(2).strip(), 'lines': []}
            tree.append(cur)
        else:
            cur['lines'].append(ln)
    return tree

def blocks(lines):
    """Lista de (tipo, contenido): p, li, sub (lista anidada), ol, quote (lista de líneas), table,
    pre (bloque de código suelto) y pre_li (bloque de código sangrado, que pertenece al elemento de lista anterior)."""
    out, para = [], []
    prev_blank = [False]
    codigo, sangrado = None, False
    def flush():
        if para:
            out.append(('p', ' '.join(para))); para.clear()
    for ln in lines:
        f = FENCE.match(ln)
        if f:
            if codigo is None:
                flush(); codigo, sangrado = [], bool(f.group(1))
            else:
                texto = textwrap.dedent('\n'.join(codigo)).strip('\n')
                out.append(('pre_li' if sangrado else 'pre', texto)); codigo = None
            continue
        if codigo is not None:
            codigo.append(ln.rstrip()); continue
        s = ln.rstrip()
        if not s.strip() or s.strip() == '---':
            flush(); prev_blank[0] = True; continue
        was_blank, prev_blank[0] = prev_blank[0], False
        if s.lstrip().startswith('|'):
            flush()
            if out and out[-1][0] == 'table': out[-1][1].append(s.strip())
            else: out.append(('table', [s.strip()]))
            continue
        m = re.match(r'^!\[([^\]]*)\]\(([^)\s]+)\)$', s.strip())
        if m:
            flush(); out.append(('img', (m.group(1), m.group(2)))); continue
        if s.startswith('!cifras'):
            flush(); out.append(('stats', s[len('!cifras'):].strip())); continue
        if s.startswith('!! '):
            flush(); out.append(('callout', s[3:].strip())); continue
        m = re.match(r'^\s*>\s?(.*)$', s)
        if m:
            flush()
            if out and out[-1][0] == 'quote' and not was_blank: out[-1][1].append(m.group(1))
            else: out.append(('quote', [m.group(1)]))
            continue
        m = re.match(r'^(\s+)[-*] (.+)$', s)
        if m: flush(); out.append(('sub', m.group(2))); continue
        m = re.match(r'^[-*] (.+)$', s)
        if m: flush(); out.append(('li', m.group(1))); continue
        m = re.match(r'^\s*(\d+)\. (.+)$', s)
        if m: flush(); out.append(('ol', m.group(2))); continue
        para.append(s.strip())
    flush()
    return out

def is_meta(t):
    return t.startswith('*') and not t.startswith('**') and t.endswith('*')

def table_rows(rows):
    cells = [[c.strip() for c in r.strip().strip('|').split('|')] for r in rows]
    return [c for c in cells if not all(re.fullmatch(r':?-+:?', x) for x in c)]

def chapter_num(title, numera=('Bloque', 'Historia')):
    """Número del capítulo si el título lo trae («Bloque 3 · …», «0. …»), y el título sin él."""
    m = re.match(r'^(Bloque|Historia) (\d+) · (.+)$', title)
    if m:
        if m.group(1) in numera: return int(m.group(2)), m.group(3)
        return None, title
    m = re.match(r'^(\d+)\. (.+)$', title)
    if m: return int(m.group(1)), m.group(2)
    return None, title

def parse(md_text, numera=('Bloque', 'Historia')):
    tree = sections(strip_frontmatter(md_text))
    data = {'titulo': '', 'subtitulo': '', 'intro': [], 'capitulos': []}
    for sec in tree:
        if sec['level'] == 1:
            data['titulo'] = sec['title']
            bl = blocks(sec['lines'])
            if bl and bl[0][0] == 'p' and is_meta(bl[0][1]):
                data['subtitulo'] = bl[0][1].strip('*')
                bl = bl[1:]
            data['intro'] = bl
        elif sec['level'] == 2:
            num, title = chapter_num(sec['title'], numera)
            data['capitulos'].append({'num': num, 'title': title, 'blocks': blocks(sec['lines'])})
        elif sec['level'] == 3 and data['capitulos']:
            cap = data['capitulos'][-1]
            cap['blocks'].append(('h', sec['title']))
            cap['blocks'].extend(blocks(sec['lines']))
    return data

# ---------------------------------------------------------------- estilo (el de la casa, del libro de CALSW)
CSS = r"""
@font-face{ font-family:'Poppins'; font-weight:400; src:url('%(F)sPoppins-Regular.ttf'); }
@font-face{ font-family:'Poppins'; font-weight:500; src:url('%(F)sPoppins-Medium.ttf'); }
@font-face{ font-family:'Poppins'; font-weight:600; src:url('%(F)sPoppins-SemiBold.ttf'); }
@font-face{ font-family:'Poppins'; font-weight:700; src:url('%(F)sPoppins-Bold.ttf'); }
@font-face{ font-family:'Lora'; font-style:normal; font-weight:400 700; src:url('%(F)sLora-VF.ttf'); }
@font-face{ font-family:'Lora'; font-style:italic; font-weight:400 700; src:url('%(F)sLora-Italic-VF.ttf'); }
@font-face{ font-family:'Mono'; src:url('%(M)s'); }
:root{ --ink:#191A23; --muted:#6E7180; --indigo:#4F46E5; --indigo-deep:#312E81;
  --indigo-50:#F1F1FB; --indigo-200:#C9C9E8; --amber:#E0833B; --amber-deep:#C26A22; --amber-50:#FCF3E9; }
@page{ size:150mm 226mm; margin:19mm 16mm 17mm 20mm; }
@page:right{ margin-left:20mm; margin-right:15mm;
  @top-right{ content:string(ct, start); font-family:'Poppins',sans-serif; font-size:7.5pt; font-weight:500;
    letter-spacing:.12em; text-transform:uppercase; color:var(--muted); padding-bottom:3mm; }
  @bottom-right{ content:counter(page); font-family:'Poppins',sans-serif; font-size:8.5pt; font-weight:600;
    color:var(--indigo-deep); padding-top:4mm; } }
@page:left{ margin-left:15mm; margin-right:20mm;
  @top-left{ content:string(ct, start); font-family:'Poppins',sans-serif; font-size:7.5pt; font-weight:500;
    letter-spacing:.12em; text-transform:uppercase; color:var(--muted); padding-bottom:3mm; }
  @bottom-left{ content:counter(page); font-family:'Poppins',sans-serif; font-size:8.5pt; font-weight:600;
    color:var(--indigo-deep); padding-top:4mm; } }
@page bare{ margin:0; @top-left{content:none} @top-right{content:none} @bottom-left{content:none} @bottom-right{content:none} }

html{ color:var(--ink); }
body{ font-family:'Lora',serif; font-size:10.5pt; line-height:1.62; text-align:justify; hyphens:auto; hyphenate-limit-chars:6 3 3; margin:0; }
p{ margin:0 0 .55em; orphans:2; widows:2; }
.body p + p{ text-indent:1.3em; margin-top:0; }
strong{ font-weight:600; } em{ font-style:italic; }
code{ font-family:'Mono','Consolas',monospace; font-size:.86em; background:var(--indigo-50); border-radius:3px; padding:0 .25em; hyphens:none; }
a{ color:var(--indigo); font-weight:600; text-decoration:none; hyphens:none; }
h2{ font-family:'Poppins',sans-serif; font-weight:600; font-size:13pt; color:var(--ink); letter-spacing:-.01em; margin:7mm 0 2.5mm; line-height:1.22; text-align:left; hyphens:none; break-after:avoid; }
h2 strong{ font-weight:600; color:var(--amber-deep); font-size:.72em; }
h3{ font-family:'Poppins',sans-serif; font-weight:600; font-size:10.2pt; color:var(--indigo-deep); margin:0 0 1mm; line-height:1.25; text-align:left; hyphens:none; break-after:avoid; }

/* PORTADA */
.cover{ page:bare; position:relative; height:226mm; box-sizing:border-box; padding:30mm 20mm 22mm; break-after:page; }
.cover::before{ content:""; position:absolute; left:0; top:0; bottom:0; width:7mm; background:var(--indigo); }
.cover::after{ content:""; position:absolute; right:22mm; bottom:80mm; width:12mm; height:12mm; border-radius:50%%; background:var(--amber); }
.cover .kicker{ font-family:'Poppins',sans-serif; font-size:9pt; font-weight:600; letter-spacing:.22em; text-transform:uppercase; color:var(--indigo); }
.cover .crule{ border:0; border-top:2px solid var(--indigo-deep); width:38mm; margin:7mm 0; }
.cover h1{ font-family:'Poppins',sans-serif; font-weight:700; line-height:1.04; letter-spacing:-.02em; color:var(--ink); margin:0; max-width:118mm; text-align:left; hyphens:none; }
.cover .sub{ font-family:'Lora',serif; font-style:italic; font-size:13.5pt; line-height:1.5; color:#3C3D4A; max-width:108mm; margin:10mm 0 0; text-align:left; hyphens:none; }
.cover .meta{ position:absolute; left:20mm; right:20mm; bottom:22mm; font-family:'Poppins',sans-serif; font-size:8.5pt; line-height:1.7; color:var(--muted); text-align:left; }
.cover .meta b{ color:var(--indigo-deep); font-weight:600; }

/* APERTURAS */
.chapter{ break-before:page; }
.opener{ padding-top:10mm; }
.opener.tight{ padding-top:0; }
.opener.tight .opener-rule{ margin-top:5mm; }
.opener.tight + *{ margin-top:5mm; }
.eyebrow{ font-family:'Poppins',sans-serif; font-size:8.5pt; font-weight:600; letter-spacing:.18em; text-transform:uppercase; color:var(--indigo); margin-bottom:6mm; }
.chap-num{ font-family:'Poppins',sans-serif; font-weight:700; font-size:68pt; line-height:.9; color:var(--indigo-deep); letter-spacing:-.02em; margin:0; text-align:left; }
.chap-num span{ color:var(--indigo); }
article[data-rh]{ string-set: ct attr(data-rh); }
h1.chap-title{ font-family:'Poppins',sans-serif; font-weight:600; font-size:24pt; line-height:1.08; color:var(--ink); letter-spacing:-.015em; margin:5mm 0 0; max-width:122mm; text-align:left; hyphens:none; }
h1.chap-title strong{ font-weight:600; font-size:.5em; color:var(--amber-deep); letter-spacing:0; display:block; margin-top:2mm; }
.opener-rule{ border:0; border-top:1.5px solid var(--indigo); width:46mm; margin:8mm 0 0; }
.opener + *{ margin-top:8mm; }
.opener.row{ padding-top:0; }
.opener.row .head{ display:flex; align-items:center; }
.opener.row .chap-num{ font-size:50pt; line-height:1; margin:0 6mm 0 0; flex:none; }
.opener.row h1.chap-title{ font-size:19pt; line-height:1.14; margin:0; flex:1; max-width:none; }
.opener.row .opener-rule{ margin-top:5mm; }
.opener.row + *{ margin-top:6mm; }
.keep{ break-inside:avoid; break-after:avoid; }
.body p.izq{ text-align:left; }
.chips{ margin:0; }
.chip{ display:inline-block; font-family:'Poppins',sans-serif; font-size:7.4pt; font-weight:500; color:var(--indigo-deep); background:var(--indigo-50); border:1px solid var(--indigo-200); border-radius:20px; padding:.9mm 3mm; margin:0 1.6mm 1.6mm 0; hyphens:none; }
.chip b{ font-weight:700; color:var(--indigo); margin-right:1mm; }
.idx{ break-inside:avoid; }
.index-label{ font-family:'Poppins',sans-serif; font-size:7.8pt; font-weight:700; letter-spacing:.12em; text-transform:uppercase; color:var(--muted); margin:5mm 0 3mm; }

/* BLOQUES */
.capitulo{ margin-top:12mm; }
.capitulo.chapter{ margin-top:0; }
.capitulo .body{ margin:0 0 3mm; }
ul.list{ list-style:none; padding:0; margin:0 0 3mm; }
ul.list > li{ position:relative; padding:0 0 0 4.5mm; margin:0 0 1.6mm; text-align:left; hyphens:manual; line-height:1.5; }
ul.list > li::before{ content:""; position:absolute; left:0; top:2.2mm; width:1.6mm; height:1.6mm; border-radius:50%%; background:var(--indigo); }
ul.list > li.sub{ margin-left:5mm; font-size:.95em; }
ul.list > li.sub::before{ background:var(--indigo-200); }
ul.list > li.eref{ padding-left:0; margin:0 0 2.6mm; border-left:2.5px solid var(--indigo-200); padding:1.2mm 0 1.2mm 4mm; }
ul.list > li.eref::before{ content:none; }
ul.list > li.eref strong:first-child{ font-family:'Poppins',sans-serif; font-weight:700; color:var(--indigo); margin-right:1.2mm; }
ol.steps{ list-style:none; padding:0; margin:0 0 3mm; counter-reset:b; }
ol.steps > li{ break-inside:avoid; position:relative; padding:0 0 0 8.5mm; margin:0 0 1.8mm; text-align:left; hyphens:manual; line-height:1.5; }
ol.steps > li::before{ counter-increment:b; content:counter(b); position:absolute; left:0; top:.4mm; width:5.6mm; height:5.6mm; border-radius:50%%;
  background:var(--amber-50); border:1.2px solid var(--amber); color:var(--amber-deep); font-family:'Poppins',sans-serif; font-weight:700; font-size:7.5pt;
  text-align:center; line-height:5.6mm; }

.lbl{ font-family:'Poppins',sans-serif; font-size:8.6pt; color:var(--indigo-deep); margin:3mm 0 1.2mm; text-align:left; break-after:avoid; }
.lbl strong{ font-weight:600; }
/* PROMPTS */
.prompt{ break-inside:avoid; margin:2.5mm 0 3.5mm; padding:3mm 4.5mm; border-left:3px solid var(--indigo); background:var(--indigo-50); border-radius:0 6px 6px 0; text-align:left; hyphens:none; font-size:9.4pt; line-height:1.5; }
.prompt p{ margin:0 0 1.4mm; } .prompt p:last-child{ margin-bottom:0; }

/* RECUADROS */
.callout{ break-inside:avoid; margin:4mm 0; padding:4mm 5mm 3.4mm; border-left:3px solid var(--amber); background:var(--amber-50); border-radius:0 6px 6px 0; text-align:left; hyphens:none; }
.callout .label{ display:block; font-family:'Poppins',sans-serif; font-size:7.8pt; font-weight:700; letter-spacing:.12em; text-transform:uppercase; color:var(--amber-deep); margin-bottom:2mm; }
.callout p{ font-size:9.8pt; line-height:1.5; margin:0; }

/* CIFRAS */
.stats{ display:flex; flex-wrap:wrap; width:100%%; margin:5mm 0 3mm; }
.stat{ box-sizing:border-box; width:23.5%%; margin:0 2%% 0 0; background:var(--indigo-50); border-radius:6px; padding:3mm 2.6mm 2.6mm; }
.stat:nth-child(4n){ margin-right:0; }
.stat.three{ width:31%%; } .stat.three:nth-child(3n){ margin-right:0; }
.stat .n{ font-family:'Poppins',sans-serif; font-weight:700; font-size:18pt; line-height:1; color:var(--indigo-deep); letter-spacing:-.02em; }
.stat .t{ font-family:'Poppins',sans-serif; font-size:6.8pt; color:var(--muted); margin-top:1.6mm; line-height:1.3; text-align:left; hyphens:none; }
.stat.amber{ background:var(--amber-50); } .stat.amber .n{ color:var(--amber-deep); }

/* CÓDIGO */
pre.code{ font-family:'Mono','Consolas',monospace; font-size:6.8pt; line-height:1.45; color:var(--ink); background:var(--indigo-50);
  border-left:3px solid var(--indigo-200); border-radius:0 6px 6px 0; padding:3mm 4mm; margin:2.5mm 0 3.5mm;
  white-space:pre-wrap; text-align:left; hyphens:none; break-inside:avoid; }
ol.steps > li pre.code, ul.list > li pre.code{ margin:2mm 0 1mm; }

/* TABLAS */
table{ width:100%%; border-collapse:collapse; margin:3.5mm 0 4.5mm; font-size:8.4pt; }
thead{ display:table-header-group; }
tr{ break-inside:avoid; }
thead th{ background:var(--indigo-deep); color:#fff; font-family:'Poppins',sans-serif; font-weight:600; font-size:7.4pt; text-align:left; padding:5px 6px; hyphens:none; line-height:1.25; vertical-align:bottom; }
tbody td{ border-bottom:1px solid var(--indigo-200); padding:5px 6px; vertical-align:top; line-height:1.35; text-align:left; hyphens:manual; font-family:'Poppins',sans-serif; font-size:7.6pt; }
tbody td:first-child{ font-weight:600; color:var(--indigo-deep); }
tbody tr:nth-child(even){ background:#FAFAFE; }

/* CAPTURAS */
figure.captura{ margin:3mm 0 4.5mm; break-inside:avoid; }
figure.captura img{ display:block; width:100%%; border:1px solid var(--indigo-200); border-radius:4px; }
figure.captura figcaption{ font-family:'Poppins',sans-serif; font-size:7pt; color:var(--muted); margin-top:1.5mm; text-align:left; hyphens:none; }
"""

def css():
    return CSS % {'F': FONTS, 'M': MONO}

# ---------------------------------------------------------------- piezas HTML
def cover_size(title):
    return 34 if len(title) > 24 else 44

def table_html(rows):
    cells = table_rows(rows)
    head, body = cells[0], cells[1:]
    return ('<table><thead><tr>' + ''.join(f'<th>{inline(h)}</th>' for h in head) + '</tr></thead><tbody>'
            + ''.join('<tr>' + ''.join(f'<td>{inline(c)}</td>' for c in r) + '</tr>' for r in body)
            + '</tbody></table>')

def quote_html(lines):
    paras, cur = [], []
    for ln in lines:
        if not ln.strip():
            if cur: paras.append(cur); cur = []
        else:
            cur.append(ln)
    if cur: paras.append(cur)
    return '<div class="prompt">' + ''.join('<p>' + '<br>'.join(inline(x) for x in p) + '</p>' for p in paras) + '</div>'

def label_body(text):
    m = re.match(r'^\*\*(.+?)\*\*\s*(.*)$', text)
    if not m: return '', text
    return m.group(1).rstrip('.:').strip(), m.group(2).strip()

def stats_html(spec):
    items = [x.strip() for x in spec.split(';') if x.strip()]
    three = ' three' if len(items) == 3 else ''
    out = []
    for n, it in enumerate(items):
        val, txt = [p.strip() for p in it.split('|', 1)]
        amber = ' amber' if n == len(items) - 1 else ''
        out.append(f'<div class="stat{three}{amber}"><div class="n">{H.escape(val)}</div><div class="t">{inline(txt)}</div></div>')
    return '<div class="stats">' + ''.join(out) + '</div>'

def pre_html(texto):
    return f'<pre class="code">{H.escape(texto, quote=False)}</pre>'

def dentro_del_ultimo(items, texto):
    """Mete el bloque de código dentro del último <li>, para que la lista siga siendo una sola."""
    items[-1] = items[-1][:-len('</li>')] + pre_html(texto) + '</li>'

def render(bl):
    groups = []
    for k, x in bl:
        kk = 'li' if k == 'sub' else k
        # Un bloque de código sangrado pertenece al elemento de lista que lo precede.
        if k == 'pre_li' and groups and groups[-1][0] in ('li', 'ol'):
            groups[-1][1].append((k, x)); continue
        if k == 'pre_li': kk = 'pre'
        if groups and groups[-1][0] == kk and kk in ('p', 'li', 'ol'): groups[-1][1].append((k, x))
        else: groups.append((kk, [(k, x)]))
    out = []
    for k, xs in groups:
        if k == 'p' and len(xs) == 1 and re.fullmatch(r'\*\*[^*]+\*\*', xs[0][1]):
            out.append(f'<p class="lbl">{inline(xs[0][1])}</p>')
        elif k == 'p':
            out.append('<div class="body">' + ''.join(
                f'<p{" class=\"izq\"" if "<a " in inline(p) else ""}>{inline(p)}</p>' for _, p in xs) + '</div>')
        elif k == 'li':
            items = []
            for kind, x in xs:
                if kind == 'pre_li' and items:
                    dentro_del_ultimo(items, x); continue
                cls = 'sub' if kind == 'sub' else ('eref' if re.match(r'^\*\*E\d\.\d\*\*', x) else '')
                items.append(f'<li{f" class={chr(34)}{cls}{chr(34)}" if cls else ""}>{inline(x)}</li>')
            out.append('<ul class="list">' + ''.join(items) + '</ul>')
        elif k == 'ol':
            items = []
            for kind, x in xs:
                if kind == 'pre_li' and items:
                    dentro_del_ultimo(items, x); continue
                items.append(f'<li>{inline(x)}</li>')
            out.append('<ol class="steps">' + ''.join(items) + '</ol>')
        elif k == 'pre':
            out.append(pre_html(xs[0][1]))
        elif k == 'quote':
            out.append(quote_html(xs[0][1]))
        elif k == 'table':
            out.append(table_html(xs[0][1]))
        elif k == 'callout':
            lab, b = label_body(xs[0][1])
            out.append(f'<div class="callout"><span class="label">{inline(lab)}</span><p>{inline(b[:1].upper() + b[1:])}</p></div>')
        elif k == 'stats':
            out.append(stats_html(xs[0][1]))
        elif k == 'h':
            out.append(f'<h2>{inline(xs[0][1])}</h2>')
        elif k == 'img':
            pie, src = xs[0][1]
            out.append(f'<figure class="captura"><img src="{H.escape(src)}" alt="{H.escape(pie)}">'
                       f'<figcaption>{inline(pie)}</figcaption></figure>')
    return out

def build(data, cfg):
    out = []
    out.append(
        f'<section class="cover"><div class="kicker">{H.escape(cfg["kicker"])}</div><hr class="crule">'
        f'<h1 style="font-size:{cover_size(data["titulo"])}pt">{H.escape(data["titulo"])}</h1>'
        f'<p class="sub">{inline(data["subtitulo"])}</p>'
        f'<div class="meta"><b>{H.escape(cfg["meta"])}</b></div></section>')

    caps = data['capitulos']
    def label(c):
        n = f'{c["num"]:02d}' if c['num'] is not None else '·'
        return f'<span class="chip"><b>{n}</b>{H.escape(plain(c["title"]))}</span>'
    out.append(
        f'<article class="chapter" data-rh="{H.escape(data["titulo"])}"><div class="opener tight">'
        f'<div class="eyebrow">{H.escape(cfg["eyebrow"])}</div>'
        f'<h1 class="chap-title">{H.escape(data["titulo"])}</h1><hr class="opener-rule"></div>'
        + ''.join(render(data['intro'])) +
        (f'<div class="idx"><div class="index-label">{H.escape(cfg["indice"])}</div>'
           f'<div class="chips">{"".join(label(c) for c in caps)}</div></div>' if cfg.get('indice') else '')
        + '</article>')

    for i, c in enumerate(caps):
        n = c['num']
        num_html = (f'<p class="chap-num">{n // 10}<span>{n % 10}</span></p>' if n is not None else '')
        head = (f'<div class="opener row"><div class="head">{num_html}'
                f'<h1 class="chap-title">{inline(c["title"])}</h1></div><hr class="opener-rule"></div>')
        partes = render(c['blocks'])
        clase = ' chapter' if (cfg.get('saltos', True) or (i == 0 and cfg.get('salto_primero', True))) else ''
        out.append(
            f'<article class="capitulo{clase}" data-rh="{H.escape(plain(c["title"]))}">' +
            (f'<div class="keep">{head}{partes[0] if partes else ""}</div>{"".join(partes[1:])}</article>' if cfg.get('keep', True)
             else f'<div class="keep">{head}</div>{"".join(partes)}</article>'))

    return ('<!DOCTYPE html><html lang="es"><head><meta charset="utf-8">'
            f'<title>{H.escape(data["titulo"])}</title><style>{css()}</style></head>'
            f'<body>{"".join(out)}</body></html>')

def main():
    png_dir = None
    if '--png' in sys.argv:
        png_dir = Path(sys.argv[sys.argv.index('--png') + 1]); png_dir.mkdir(parents=True, exist_ok=True)
    from weasyprint import HTML
    solo = sys.argv[sys.argv.index('--v') + 1] if '--v' in sys.argv else None
    for cfg in DOCS:
        if solo and str(cfg.get('v', 1)) != solo: continue
        data = parse((DEV / cfg['md']).read_text(encoding='utf-8'), cfg.get('numera', ('Bloque', 'Historia')))
        html = build(data, cfg)
        pdf_path = OUT / cfg['pdf']
        HTML(string=html, base_url=str(DEV)).write_pdf(str(pdf_path))
        n = '?'
        if png_dir:
            import fitz
            doc = fitz.open(str(pdf_path)); n = len(doc)
            sub = png_dir / Path(cfg['pdf']).stem; sub.mkdir(exist_ok=True)
            for i, page in enumerate(doc):
                page.get_pixmap(dpi=60).save(str(sub / f'p{i + 1:02d}.png'))
        print(f'{pdf_path.name}  paginas={n}')

if __name__ == '__main__':
    main()
