"""Renders assets/logo.svg into PNGs and assets/IdleWalker.ico.

Needs only Python and Chrome (or Edge): the browser rasterises the
SVG headlessly, and the .ico is assembled here from PNG frames. Sizes up to
32 px use assets/logo-small.svg, which drops the detail that would turn to mud.

    python tools/make-logo.py
"""
import os, shutil, struct, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
VIEW = 512
ICON_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
BROWSERS = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]

def browser():
    for b in BROWSERS:
        if os.path.exists(b):
            return b
    sys.exit("Edge or Chrome not found.")

def render(svg, size, out, work):
    page = os.path.join(work, "page.html")
    with open(svg, encoding="utf-8") as f:
        markup = f.read()
    with open(page, "w", encoding="utf-8") as f:
        f.write('<html><body style="margin:0;background:transparent">'
                '<div style="width:%dpx;height:%dpx">%s</div></body></html>'
                % (VIEW, VIEW, markup.replace('width="256" height="256"',
                                              'width="%d" height="%d"' % (VIEW, VIEW))))
    # Headless windows have a minimum size, so lay out at VIEW px and let the
    # device scale factor shrink the screenshot to the size wanted.
    subprocess.run([browser(), "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    "--default-background-color=00000000", "--force-device-scale-factor=%g" % (size / float(VIEW)),
                    "--user-data-dir=" + os.path.join(work, "profile"),
                    "--window-size=%d,%d" % (VIEW, VIEW), "--screenshot=" + out,
                    "file:///" + page.replace("\\", "/")],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

def write_ico(frames, out):
    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries, blobs = b"", b""
    for size, data in frames:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)
    with open(out, "wb") as f:
        f.write(header + entries + blobs)

def main():
    work = tempfile.mkdtemp()
    try:
        frames = []
        for size in ICON_SIZES:
            svg = os.path.join(ASSETS, "logo-small.svg" if size <= 32 else "logo.svg")
            png = os.path.join(work, "%d.png" % size)
            render(svg, size, png, work)
            with open(png, "rb") as f:
                frames.append((size, f.read()))
        write_ico(frames, os.path.join(ASSETS, "IdleWalker.ico"))
        render(os.path.join(ASSETS, "logo.svg"), 512, os.path.join(ASSETS, "logo.png"), work)
        print("Wrote assets/IdleWalker.ico (%s) and assets/logo.png"
              % ", ".join(str(s) for s in ICON_SIZES))
    finally:
        shutil.rmtree(work, ignore_errors=True)

if __name__ == "__main__":
    main()
