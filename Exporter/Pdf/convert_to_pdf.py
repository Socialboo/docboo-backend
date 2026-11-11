"""
Converts a .docx to PDF with LibreOffice, updating indexes (table of contents, lists of figures and
tables) and fields first. LibreOffice does not recalculate them when importing a .docx, so a plain
`soffice --convert-to pdf` would print the placeholder entries without page numbers.

Usage: python convert_to_pdf.py <pipe-name> <input.docx> <output.pdf>
The soffice instance must already be listening on: --accept=pipe,name=<pipe-name>;urp;
Runs with LibreOffice's bundled Python (Windows/macOS) or a system python3 with the uno module.
"""
import sys
import time

import uno
from com.sun.star.beans import PropertyValue
from com.sun.star.connection import NoConnectException


def prop(name, value):
    p = PropertyValue()
    p.Name = name
    p.Value = value
    return p


def connect(pipe, timeout_seconds=60):
    local = uno.getComponentContext()
    resolver = local.ServiceManager.createInstanceWithContext("com.sun.star.bridge.UnoUrlResolver", local)
    deadline = time.time() + timeout_seconds
    while True:
        try:
            return resolver.resolve(f"uno:pipe,name={pipe};urp;StarOffice.ComponentContext")
        except NoConnectException:
            if time.time() > deadline:
                raise
            time.sleep(0.2)


def main():
    pipe, source, target = sys.argv[1:4]
    ctx = connect(pipe)
    desktop = ctx.ServiceManager.createInstanceWithContext("com.sun.star.frame.Desktop", ctx)
    doc = desktop.loadComponentFromURL(uno.systemPathToFileUrl(source), "_blank", 0,
                                       (prop("Hidden", True), prop("ReadOnly", False)))
    if doc is None:
        print("could not load document", file=sys.stderr)
        return 2
    try:
        indexes = doc.getDocumentIndexes()
        # Two passes: the first fills the entries, which can move pages; the second fixes the numbers.
        for _ in range(2):
            for i in range(indexes.getCount()):
                indexes.getByIndex(i).update()
            doc.getTextFields().refresh()
        doc.storeToURL(uno.systemPathToFileUrl(target), (prop("FilterName", "writer_pdf_Export"),))
        print(f"indexes={indexes.getCount()}")
    finally:
        doc.close(True)
        try:
            desktop.terminate()
        except Exception:  # the connection drops while LibreOffice shuts down
            pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
