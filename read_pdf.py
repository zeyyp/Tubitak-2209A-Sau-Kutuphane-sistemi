import pdfplumber
pdf = pdfplumber.open('zeynep_uysal_projesi.pdf')
text = '\n'.join(p.extract_text() or '' for p in pdf.pages)
pdf.close()
print(text)
