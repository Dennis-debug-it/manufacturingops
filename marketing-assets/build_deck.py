# -*- coding: utf-8 -*-
"""Builds the ECOSYS ServiceOps marketing deck."""
from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
from pptx.oxml.ns import qn
import copy

# ---------- Brand palette ----------
NAVY = RGBColor(0x0F, 0x3B, 0x5E)
NAVY_LIGHT = RGBColor(0x1A, 0x52, 0x76)
ACCENT = RGBColor(0x2E, 0x86, 0xAB)
GREEN = RGBColor(0x27, 0xAE, 0x60)
AMBER = RGBColor(0xF3, 0x9C, 0x12)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
LGRAY = RGBColor(0xF8, 0xF9, 0xFA)
DTEXT = RGBColor(0x2C, 0x3E, 0x50)
MUTED = RGBColor(0xB8, 0xC7, 0xD3)

FONT = "Calibri"

prs = Presentation()
prs.slide_width = Inches(10)
prs.slide_height = Inches(5.625)
BLANK = prs.slide_layouts[6]

SW, SH = 10.0, 5.625


def add_slide(bg=WHITE):
    s = prs.slides.add_slide(BLANK)
    rect = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(0), Inches(0), prs.slide_width, prs.slide_height)
    rect.fill.solid()
    rect.fill.fore_color.rgb = bg
    rect.line.fill.background()
    rect.shadow.inherit = False
    # send to back
    sp = rect._element
    sp.getparent().remove(sp)
    s.shapes._spTree.insert(2, sp)
    return s


def no_shadow(shape):
    shape.shadow.inherit = False


def add_textbox(slide, x, y, w, h, text, size=14, bold=False, italic=False,
                 color=DTEXT, align=PP_ALIGN.LEFT, font=FONT, anchor=None, line_spacing=None):
    tb = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
    tf = tb.text_frame
    tf.word_wrap = True
    if anchor:
        tf.vertical_anchor = anchor
    lines = text.split("\n")
    for i, line in enumerate(lines):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = align
        if line_spacing:
            p.line_spacing = line_spacing
        r = p.add_run()
        r.text = line
        r.font.size = Pt(size)
        r.font.bold = bold
        r.font.italic = italic
        r.font.color.rgb = color
        r.font.name = font
    return tb


def add_rounded_rect(slide, x, y, w, h, fill=WHITE, line_color=None, radius=0.08):
    shp = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(x), Inches(y), Inches(w), Inches(h))
    try:
        shp.adjustments[0] = radius
    except Exception:
        pass
    shp.fill.solid()
    shp.fill.fore_color.rgb = fill
    if line_color is None:
        shp.line.fill.background()
    else:
        shp.line.color.rgb = line_color
        shp.line.width = Pt(0.75)
    no_shadow(shp)
    return shp


def add_circle(slide, cx, cy, d, fill=ACCENT):
    shp = slide.shapes.add_shape(MSO_SHAPE.OVAL, Inches(cx - d / 2), Inches(cy - d / 2), Inches(d), Inches(d))
    shp.fill.solid()
    shp.fill.fore_color.rgb = fill
    shp.line.fill.background()
    no_shadow(shp)
    return shp


def add_icon_circle(slide, cx, cy, d, label, fill=ACCENT, size=18, color=WHITE):
    add_circle(slide, cx, cy, d, fill)
    tb = slide.shapes.add_textbox(Inches(cx - d / 2), Inches(cy - d / 2), Inches(d), Inches(d))
    tf = tb.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = MSO_ANCHOR.MIDDLE
    p = tf.paragraphs[0]
    p.alignment = PP_ALIGN.CENTER
    r = p.add_run()
    r.text = label
    r.font.size = Pt(size)
    r.font.bold = True
    r.font.color.rgb = color
    r.font.name = FONT
    return tb


def add_placeholder_image(slide, x, y, w, h, label, bg=RGBColor(0xE4, 0xEC, 0xF1)):
    box = add_rounded_rect(slide, x, y, w, h, fill=bg, line_color=ACCENT, radius=0.04)
    tf = box.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = MSO_ANCHOR.MIDDLE
    p = tf.paragraphs[0]
    p.alignment = PP_ALIGN.CENTER
    r = p.add_run()
    r.text = "\U0001F4F7  " + label
    r.font.size = Pt(12)
    r.font.italic = True
    r.font.color.rgb = ACCENT
    r.font.name = FONT
    return box


def add_line(slide, x1, y1, x2, y2, color=MUTED, weight=1.5):
    ln = slide.shapes.add_connector(1, Inches(x1), Inches(y1), Inches(x2), Inches(y2))
    ln.line.color.rgb = color
    ln.line.width = Pt(weight)
    return ln


def add_slide_title(slide, text, dark_bg=True, x=0.5, y=0.35, w=9.0, size=32):
    color = WHITE if dark_bg else NAVY
    return add_textbox(slide, x, y, w, 0.7, text, size=size, bold=True, color=color)


def add_footer(slide, dark_bg=True):
    color = MUTED if dark_bg else RGBColor(0x8A, 0x9B, 0xA8)
    add_textbox(slide, 0.5, SH - 0.4, 6.0, 0.3, "ECOSYS ServiceOps", size=9, color=color)
    add_textbox(slide, SW - 2.5, SH - 0.4, 2.0, 0.3, "Confidential", size=9, color=color, align=PP_ALIGN.RIGHT)


# =========================================================
# SLIDE 1 — TITLE
# =========================================================
s = add_slide(NAVY)
add_textbox(s, 0.6, 0.7, 6.5, 1.0, "ECOSYS", size=48, bold=True, color=WHITE)
add_textbox(s, 0.6, 1.45, 6.5, 0.6, "ServiceOps", size=30, bold=True, color=ACCENT)
add_textbox(s, 0.6, 2.15, 6.0, 0.5, "Connect  •  Automate  •  Scale  •  Rely.",
            size=18, italic=True, color=WHITE)
add_placeholder_image(s, 5.9, 0.75, 3.6, 3.15, "Dashboard", bg=RGBColor(0x18, 0x4A, 0x70))
add_rounded_rect(s, 0.0, 4.95, 10.0, 0.4, fill=NAVY_LIGHT, radius=0.0)
add_textbox(s, 0.5, 4.98, 6.0, 0.35, "Enterprise Field Service Management Platform", size=12, color=LGRAY)
add_textbox(s, 6.5, 4.98, 3.0, 0.35, "www.ecosysdigital.co.ke", size=12, color=LGRAY, align=PP_ALIGN.RIGHT)

# =========================================================
# SLIDE 2 — THE CHALLENGE
# =========================================================
s = add_slide(NAVY)
add_slide_title(s, "The Challenge")
cards = [
    ("Disconnected Teams", "Technicians, dispatchers and managers working in silos with no real-time visibility."),
    ("Manual Processes", "Paper-based work orders, spreadsheet tracking and missed preventive maintenance."),
    ("No Business Intelligence", "No insight into SLA compliance, technician performance or client profitability."),
]
cx0 = 0.55
cw = 2.9
gap = 0.25
for i, (title, desc) in enumerate(cards):
    x = cx0 + i * (cw + gap)
    add_rounded_rect(s, x, 1.35, cw, 3.1, fill=NAVY_LIGHT, radius=0.06)
    add_icon_circle(s, x + cw / 2, 1.9, 0.85, "!", fill=AMBER, size=28)
    add_textbox(s, x + 0.2, 2.5, cw - 0.4, 0.7, title, size=16, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
    add_textbox(s, x + 0.25, 3.15, cw - 0.5, 1.2, desc, size=12, color=MUTED, align=PP_ALIGN.CENTER)
add_footer(s, True)

# =========================================================
# SLIDE 3 — THE SOLUTION
# =========================================================
s = add_slide(WHITE)
add_slide_title(s, "The Solution", dark_bg=False)
stats = [("100%", "Paperless operations"), ("Real-time", "Field visibility"), ("One platform", "End to end")]
y = 1.35
for label, desc in stats:
    add_textbox(s, 0.55, y, 4.0, 0.85, label, size=42, bold=True, color=ACCENT)
    add_textbox(s, 0.6, y + 0.75, 4.0, 0.35, desc, size=13, color=DTEXT)
    y += 1.25
add_placeholder_image(s, 5.0, 1.3, 4.45, 3.6, "Work Orders List")
add_footer(s, False)

# =========================================================
# SLIDE 4 — PLATFORM OVERVIEW
# =========================================================
s = add_slide(LGRAY)
add_slide_title(s, "Platform Overview", dark_bg=False)
modules = [
    ("WO", "Work Orders", "Full lifecycle job management"),
    ("PM", "Preventive Maintenance", "Automated scheduling engine"),
    ("C&A", "Clients & Assets", "Complete hierarchy tracking"),
    ("INV", "Inventory & Stores", "Parts and van stock control"),
    ("FIN", "Finance & Billing", "Quotes, invoices, payments"),
    ("T&A", "Team & Attendance", "GPS clock-in / clock-out"),
    ("R&A", "Reports & Analytics", "Executive KPI dashboards"),
    ("CP", "Client Portal", "Self-service for customers"),
]
cols, rows = 4, 2
mw, mh = 2.15, 1.75
mx0, my0 = 0.45, 1.25
gapx, gapy = 0.15, 0.15
for idx, (icon, name, desc) in enumerate(modules):
    r, c = divmod(idx, cols)
    x = mx0 + c * (mw + gapx)
    y = my0 + r * (mh + gapy)
    add_rounded_rect(s, x, y, mw, mh, fill=WHITE, radius=0.08)
    add_icon_circle(s, x + mw / 2, y + 0.42, 0.55, icon, fill=ACCENT, size=11)
    add_textbox(s, x + 0.1, y + 0.78, mw - 0.2, 0.4, name, size=11.5, bold=True, color=NAVY, align=PP_ALIGN.CENTER)
    add_textbox(s, x + 0.12, y + 1.18, mw - 0.24, 0.5, desc, size=9, color=DTEXT, align=PP_ALIGN.CENTER)
add_footer(s, False)

# =========================================================
# SLIDE 5 — WORK ORDERS
# =========================================================
s = add_slide(WHITE)
add_rounded_rect(s, 0, 0, 10, 0.95, fill=NAVY, radius=0.0)
add_textbox(s, 0.5, 0.2, 8.0, 0.6, "Work Orders", size=28, bold=True, color=WHITE)
add_placeholder_image(s, 0.5, 1.2, 4.3, 3.9, "Work Order Detail")
feats = [
    "Full work order lifecycle (New → Assigned → In Progress → Completed → Closed)",
    "Interactive PM checklists with Pass/Fail outcomes",
    "Photo evidence capture with offline support",
    "Digital signature collection",
    "Materials tracking with van stock integration",
    "Automated service report generation",
    "Invoice creation from completed reports",
]
y = 1.3
for f in feats:
    add_icon_circle(s, 5.25, y + 0.14, 0.28, "✓", fill=GREEN, size=12)
    add_textbox(s, 5.55, y, 4.0, 0.55, f, size=11.5, color=DTEXT, line_spacing=1.0)
    y += 0.56
add_footer(s, False)

# =========================================================
# SLIDE 6 — PREVENTIVE MAINTENANCE
# =========================================================
s = add_slide(WHITE)
add_slide_title(s, "Preventive Maintenance", dark_bg=False)
stats6 = [("PM Engine", "Fully automated scheduling"), ("Zero Missed", "Maintenance events"), ("Coverage", "Contract or standalone")]
sw = 2.95
for i, (label, desc) in enumerate(stats6):
    x = 0.5 + i * (sw + 0.15)
    add_rounded_rect(s, x, 1.15, sw, 1.15, fill=LGRAY, radius=0.08)
    add_textbox(s, x, 1.22, sw, 0.55, label, size=22, bold=True, color=ACCENT, align=PP_ALIGN.CENTER)
    add_textbox(s, x, 1.75, sw, 0.4, desc, size=11, color=DTEXT, align=PP_ALIGN.CENTER)
add_placeholder_image(s, 0.5, 2.55, 4.3, 2.6, "PM Calendar / Schedules")
caps = [
    "Calendar-based and usage-based triggers",
    "Contract-linked PM schedules",
    "Auto-generates work orders in advance",
    "Compliance tracking and reporting",
    "Failed task → automatic follow-up WO",
]
y = 2.6
for c in caps:
    add_textbox(s, 5.1, y, 0.25, 0.4, "•", size=13, bold=True, color=ACCENT)
    add_textbox(s, 5.35, y, 4.15, 0.45, c, size=12, color=DTEXT)
    y += 0.47
add_footer(s, False)

# =========================================================
# SLIDE 7 — CLIENT & ASSET MANAGEMENT
# =========================================================
s = add_slide(LGRAY)
add_slide_title(s, "Client & Asset Management", dark_bg=False)
cols7 = [
    ("CLIENT", "Company profile, key contacts, linked sites and contracts"),
    ("SITE", "Address, map location, assets installed on premises"),
    ("ASSET", "Serial number, warranty status, full service history"),
]
cw7 = 2.9
for i, (name, desc) in enumerate(cols7):
    x = 0.55 + i * (cw7 + 0.25)
    add_rounded_rect(s, x, 1.2, cw7, 1.9, fill=WHITE, radius=0.08)
    add_icon_circle(s, x + cw7 / 2, 1.65, 0.6, name[0], fill=ACCENT, size=18)
    add_textbox(s, x, 2.0, cw7, 0.35, name, size=14, bold=True, color=NAVY, align=PP_ALIGN.CENTER)
    add_textbox(s, x + 0.2, 2.35, cw7 - 0.4, 0.7, desc, size=10.5, color=DTEXT, align=PP_ALIGN.CENTER)
    if i < 2:
        add_line(s, x + cw7 + 0.03, 2.15, x + cw7 + 0.22, 2.15, color=ACCENT, weight=2.0)
add_placeholder_image(s, 0.55, 3.3, 8.9, 1.75, "Client / Asset Detail")
add_textbox(s, 0.55, 5.1, 8.9, 0.35, "Complete visibility from client to individual asset",
            size=12, italic=True, color=NAVY, align=PP_ALIGN.CENTER)

# =========================================================
# SLIDE 8 — FINANCE & BILLING
# =========================================================
s = add_slide(NAVY)
add_slide_title(s, "Finance & Billing")
steps = ["Work\nCompleted", "Report\nGenerated", "Invoice\nCreated", "Payment\nReceived"]
sx, sy, sd = 0.6, 1.5, 1.1
for i, st in enumerate(steps):
    x = sx + i * 1.55
    add_circle(s, x + sd / 2, sy + sd / 2, sd, fill=ACCENT)
    tb = s.shapes.add_textbox(Inches(x), Inches(sy), Inches(sd), Inches(sd))
    tf = tb.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = MSO_ANCHOR.MIDDLE
    p = tf.paragraphs[0]
    p.alignment = PP_ALIGN.CENTER
    r = p.add_run()
    r.text = str(i + 1)
    r.font.size = Pt(24)
    r.font.bold = True
    r.font.color.rgb = WHITE
    r.font.name = FONT
    add_textbox(s, x - 0.2, sy + sd + 0.1, sd + 0.4, 0.6, st, size=11, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
    if i < 3:
        add_line(s, x + sd + 0.05, sy + sd / 2, x + 1.55, sy + sd / 2, color=AMBER, weight=2.5)
add_placeholder_image(s, 0.6, 3.35, 4.9, 1.9, "Invoice / Finance Page", bg=RGBColor(0x18, 0x4A, 0x70))
finstats = [("Automated", "Billing from contracts"), ("Multi-currency", "Invoicing"), ("M-Pesa + Card", "Payment collection")]
y = 1.5
for label, desc in finstats:
    add_rounded_rect(s, 5.85, y, 3.6, 1.05, fill=WHITE, radius=0.1)
    add_textbox(s, 5.85, y + 0.08, 3.6, 0.45, label, size=17, bold=True, color=ACCENT, align=PP_ALIGN.CENTER)
    add_textbox(s, 5.85, y + 0.55, 3.6, 0.4, desc, size=11, color=DTEXT, align=PP_ALIGN.CENTER)
    y += 1.2

# =========================================================
# SLIDE 9 — REPORTING & ANALYTICS
# =========================================================
s = add_slide(WHITE)
add_slide_title(s, "Reporting & Analytics", dark_bg=False)
add_placeholder_image(s, 0.5, 1.15, 9.0, 2.5, "Executive Dashboard")
metrics = [("SLA Compliance", "%"), ("Tech Utilisation", "%"), ("PM Compliance", "%"), ("Outstanding AR", "KES")]
mw9 = 2.1
for i, (name, unit) in enumerate(metrics):
    x = 0.5 + i * (mw9 + 0.12)
    add_rounded_rect(s, x, 3.85, mw9, 1.0, fill=LGRAY, radius=0.1)
    add_textbox(s, x, 3.92, mw9, 0.4, name, size=11, bold=True, color=NAVY, align=PP_ALIGN.CENTER)
    add_textbox(s, x, 4.32, mw9, 0.4, unit, size=13, color=ACCENT, align=PP_ALIGN.CENTER, bold=True)
add_textbox(s, 0.5, 5.0, 9.0, 0.35,
            "Executive dashboard with configurable KPIs, period comparisons and real-time alerts",
            size=11, italic=True, color=DTEXT, align=PP_ALIGN.CENTER)

# =========================================================
# SLIDE 10 — MOBILE & FIELD EXPERIENCE
# =========================================================
s = add_slide(NAVY)
add_slide_title(s, "Mobile & Field Experience")
add_placeholder_image(s, 0.55, 1.3, 2.4, 3.9, "Mobile App", bg=RGBColor(0x18, 0x4A, 0x70))
mobile_feats = [
    ("GPS", "GPS clock-in / clock-out with geo-fencing"),
    ("CHK", "Interactive checklists in the field"),
    ("CAM", "Photo evidence capture offline"),
    ("SIG", "Digital signature collection"),
    ("VAN", "Van stock and materials logging"),
    ("SYN", "Works fully offline — syncs on reconnect"),
]
y = 1.35
for icon, txt in mobile_feats:
    add_icon_circle(s, 3.55, y + 0.24, 0.5, icon, fill=ACCENT, size=9)
    add_textbox(s, 3.95, y, 5.6, 0.55, txt, size=12.5, color=WHITE)
    y += 0.66
add_footer(s, True)

# =========================================================
# SLIDE 11 — CLIENT PORTAL
# =========================================================
s = add_slide(LGRAY)
add_slide_title(s, "Client Portal", dark_bg=False)
add_placeholder_image(s, 0.5, 1.3, 4.1, 3.85, "Client Portal")
add_textbox(s, 4.9, 1.25, 4.6, 0.5, "Give clients full visibility", size=20, bold=True, color=NAVY)
portal_items = [
    "Submit service requests online",
    "Track technician arrival in real time",
    "Download approved service reports",
    "View and pay invoices online (M-Pesa / Card)",
    "Accept or decline quotes online",
    "Acknowledge completed work",
    "Receive CSAT survey after job close",
]
y = 1.85
for item in portal_items:
    add_textbox(s, 4.95, y, 0.25, 0.4, "•", size=13, bold=True, color=ACCENT)
    add_textbox(s, 5.2, y, 4.3, 0.4, item, size=12, color=DTEXT)
    y += 0.44
add_footer(s, False)

# =========================================================
# SLIDE 12 — MULTI-TENANT PLATFORM
# =========================================================
s = add_slide(NAVY)
add_slide_title(s, "Multi-Tenant Platform")
add_rounded_rect(s, 3.5, 1.3, 3.0, 0.7, fill=ACCENT, radius=0.15)
add_textbox(s, 3.5, 1.45, 3.0, 0.45, "ECOSYS Platform", size=16, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
tenant_labels = ["Tenant A", "Tenant B", "Tenant C"]
for i, t in enumerate(tenant_labels):
    x = 2.35 + i * 1.85
    add_rounded_rect(s, x, 2.5, 1.55, 0.85, fill=NAVY_LIGHT, radius=0.12)
    add_textbox(s, x, 2.65, 1.55, 0.55, t, size=13, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
    add_line(s, x + 0.775, 2.0, x + 0.775, 2.5, color=ACCENT, weight=2.0)
features12 = ["White-label branding", "Feature flags per plan", "Onboarding automation",
              "Usage monitoring", "CRM pipeline", "Billing management"]
fx = [0.5, 3.65, 6.8]
for i, f in enumerate(features12):
    col = i % 3
    row = i // 3
    x = fx[col]
    y = 3.7 + row * 0.55
    add_rounded_rect(s, x, y, 2.6, 0.45, fill=NAVY_LIGHT, radius=0.2)
    add_textbox(s, x, y + 0.06, 2.6, 0.35, f, size=10.5, color=WHITE, align=PP_ALIGN.CENTER)

# =========================================================
# SLIDE 13 — SECURITY & COMPLIANCE
# =========================================================
s = add_slide(WHITE)
add_slide_title(s, "Security & Compliance", dark_bg=False)
add_textbox(s, 0.55, 1.15, 4.2, 0.4, "Security", size=15, bold=True, color=NAVY)
sec_items = [
    "Role-based access control (6 tenant roles)",
    "JWT authentication with refresh",
    "Multi-tenant data isolation",
    "Full audit trail on all actions",
    "Impersonation with audit logging",
    "Platform security policies",
]
y = 1.65
for it in sec_items:
    add_icon_circle(s, 0.75, y + 0.15, 0.26, "•", fill=NAVY, size=9)
    add_textbox(s, 1.0, y, 3.9, 0.42, it, size=11.5, color=DTEXT)
    y += 0.46
add_textbox(s, 5.15, 1.15, 4.2, 0.4, "Reliability", size=15, bold=True, color=NAVY)
rel_items = [
    "Background job processing",
    "Automated email sequences",
    "Real-time sync",
    "System health monitoring",
    "Multi-currency, multi-timezone",
    "PWA — installs as native app",
]
y = 1.65
for it in rel_items:
    add_icon_circle(s, 5.35, y + 0.15, 0.26, "•", fill=ACCENT, size=9)
    add_textbox(s, 5.6, y, 3.9, 0.42, it, size=11.5, color=DTEXT)
    y += 0.46
add_footer(s, False)

# =========================================================
# SLIDE 14 — WHY ECOSYS
# =========================================================
s = add_slide(LGRAY)
add_slide_title(s, "Why ECOSYS", dark_bg=False)
benefits = [
    ("Purpose-Built", "Designed for field service teams in Africa and beyond"),
    ("End-to-End", "From first call to final payment in one platform"),
    ("Mobile-First", "Works offline, syncs automatically"),
    ("White-Label Ready", "Your brand, your portal, your experience"),
    ("Scales with You", "Starter to Enterprise, pay as you grow"),
    ("Implementation Support", "Guided onboarding and dedicated support"),
]
bw, bh = 2.9, 1.55
bx0, by0 = 0.45, 1.2
for idx, (title, desc) in enumerate(benefits):
    r, c = divmod(idx, 3)
    x = bx0 + c * (bw + 0.15)
    y = by0 + r * (bh + 0.15)
    add_rounded_rect(s, x, y, bw, bh, fill=WHITE, radius=0.08)
    add_icon_circle(s, x + 0.5, y + 0.45, 0.55, str(idx + 1), fill=ACCENT, size=16)
    add_textbox(s, x + 0.2, y + 0.85, bw - 0.4, 0.3, title, size=13, bold=True, color=NAVY)
    add_textbox(s, x + 0.2, y + 1.13, bw - 0.4, 0.4, desc, size=9.5, color=DTEXT)
add_footer(s, False)

# =========================================================
# SLIDE 15 — CALL TO ACTION
# =========================================================
s = add_slide(NAVY)
add_textbox(s, 0.5, 1.1, 9.0, 0.8, "Ready to transform your operations?",
            size=32, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
add_textbox(s, 0.5, 1.85, 9.0, 0.5, "Connect  •  Automate  •  Scale  •  Rely.",
            size=18, italic=True, color=ACCENT, align=PP_ALIGN.CENTER)

btn1 = add_rounded_rect(s, 2.7, 2.7, 2.1, 0.6, fill=ACCENT, radius=0.5)
add_textbox(s, 2.7, 2.83, 2.1, 0.4, "Request a Demo", size=13, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
btn2 = add_rounded_rect(s, 5.2, 2.7, 2.1, 0.6, fill=WHITE, radius=0.5)
add_textbox(s, 5.2, 2.83, 2.1, 0.4, "Start Free Trial", size=13, bold=True, color=NAVY, align=PP_ALIGN.CENTER)

add_textbox(s, 0.5, 3.7, 9.0, 0.35, "Dennis Yegon  •  0726 645 667", size=13, color=WHITE, align=PP_ALIGN.CENTER)
add_textbox(s, 0.5, 4.05, 9.0, 0.35, "support@ecosysdigital.co.ke  •  www.ecosysdigital.co.ke",
            size=13, color=MUTED, align=PP_ALIGN.CENTER)
add_textbox(s, 0.5, 4.85, 9.0, 0.5, "ECOSYS", size=22, bold=True, color=WHITE, align=PP_ALIGN.CENTER)

prs.save(r"C:\ecosys\marketing-assets\ECOSYS_ServiceOps_Marketing.pptx")
print("Saved deck with", len(prs.slides.__iter__.__self__._sldIdLst), "slides")
