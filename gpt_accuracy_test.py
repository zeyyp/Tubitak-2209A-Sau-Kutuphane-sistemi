"""
IP-5 Doğruluk Testi: ChatGPT geri bildirim analizi
Başarı ölçütü: %90 doğruluk (30 testten 27'si doğru)
"""
import json, sys
from urllib import request as urlrequest

API_KEY = sys.argv[1]
MODEL   = "gpt-3.5-turbo"

TEST_CASES = [
    {"id": 1,  "text": "Masalar çok gürültülü, konsantre olamıyorum.",                                         "expected": "Negatif"},
    {"id": 2,  "text": "Rezervasyon sistemi bazen yavaş çalışıyor.",                                           "expected": "Negatif"},
    {"id": 3,  "text": "Kütüphanede yeterli priz yok, bilgisayarım şarj olmuyor.",                            "expected": "Negatif"},
    {"id": 4,  "text": "Rezervasyon sistemi sayesinde artık yer bulamama sorunu yaşamıyorum.",                 "expected": "Pozitif"},
    {"id": 5,  "text": "Uygulama çok kullanışlı, kolayca rezervasyon yapabiliyorum.",                         "expected": "Pozitif"},
    {"id": 6,  "text": "Akademik öncelik sistemi çok adil buluyorum.",                                         "expected": "Pozitif"},
    {"id": 7,  "text": "Mobil uygulama da olsa iyi olurdu.",                                                   "expected": "Nötr"},
    {"id": 8,  "text": "Rezervasyon süresi 4 saatten fazla da olabilir.",                                      "expected": "Nötr"},
    {"id": 9,  "text": "Farklı kat seçeneği eklenebilir.",                                                     "expected": "Nötr"},
    {"id": 10, "text": "Masa özelliklerine göre filtreleme olsa güzel olurdu.",                                "expected": "Nötr"},
    {"id": 11, "text": "Bildirim sistemi eklenebilir, rezervasyon saatini hatırlatsın.",                       "expected": "Nötr"},
    {"id": 12, "text": "Grup çalışma odaları da eklenebilir.",                                                 "expected": "Nötr"},
    {"id": 13, "text": "Artık kütüphaneye gelmeden önce yerim olduğunu biliyorum, harika.",                   "expected": "Pozitif"},
    {"id": 14, "text": "Turnike sistemi giriş çıkışı çok hızlandırdı.",                                       "expected": "Pozitif"},
    {"id": 15, "text": "Arayüz çok sade ve anlaşılır.",                                                       "expected": "Pozitif"},
    {"id": 16, "text": "Klima çok fazla açık, üşüyoruz.",                                                     "expected": "Negatif"},
    {"id": 17, "text": "Bazı masalar sallanıyor, değiştirilmesi lazım.",                                       "expected": "Negatif"},
    {"id": 18, "text": "Rezervasyon iptal etmek çok karmaşık.",                                               "expected": "Negatif"},
    {"id": 19, "text": "Giriş turnikesi bazen okutmuyor.",                                                     "expected": "Negatif"},
    {"id": 20, "text": "Kütüphane çok erken kapanıyor.",                                                       "expected": "Negatif"},
    {"id": 21, "text": "Temizlik yetersiz, masalar kirli geliyor.",                                            "expected": "Negatif"},
    {"id": 22, "text": "Wi-Fi bağlantısı zayıf.",                                                             "expected": "Negatif"},
    {"id": 23, "text": "Sınav döneminde öncelik verilmesi çok mantıklı bir uygulama.",                        "expected": "Pozitif"},
    {"id": 24, "text": "Sistem çok stabil çalışıyor, hiç sorun yaşamadım.",                                   "expected": "Pozitif"},
    {"id": 25, "text": "Önceki kaosa göre çok büyük gelişme.",                                                "expected": "Pozitif"},
    {"id": 26, "text": "Geri bildirim gönderebilmek güzel, sesimiz duyuluyor.",                               "expected": "Pozitif"},
    {"id": 27, "text": "Farklı dil seçeneği olsa iyi olurdu.",                                                "expected": "Nötr"},
    {"id": 28, "text": "Rezervasyon geçmişini görmek istiyorum.",                                             "expected": "Nötr"},
    {"id": 29, "text": "Kütüphane haritası üzerinden masa seçimi yapılabilir.",                               "expected": "Nötr"},
    {"id": 30, "text": "Arkadaşlarla aynı bölge için ortak rezervasyon yapılabilse iyi olur.",               "expected": "Nötr"},
]

SYSTEM_MSG = "Sen yardımcı bir kütüphane yönetim asistanısın. Her zaman Türkçe yanıt verirsin."

def classify(text):
    prompt = (
        "Aşağıdaki öğrenci geri bildirimini analiz et ve yalnızca şu üç kelimeden birini yaz: "
        "Pozitif, Negatif veya Nötr\n"
        "Öneri veya istek içeren tarafsız cümleler Nötr kategorisine girer.\n\n"
        f"Geri bildirim: {text}\n\nYanıt (yalnızca tek kelime):"
    )
    body = json.dumps({
        "model": MODEL,
        "messages": [
            {"role": "system", "content": SYSTEM_MSG},
            {"role": "user",   "content": prompt},
        ],
        "temperature": 0,
        "max_tokens": 5,
    }).encode("utf-8")

    req = urlrequest.Request(
        "https://api.openai.com/v1/chat/completions",
        data=body,
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {API_KEY}",
        },
        method="POST",
    )
    with urlrequest.urlopen(req, timeout=20) as resp:
        data = json.loads(resp.read())

    return data["choices"][0]["message"]["content"].strip()

print("=" * 60)
print("IP-5 GPT Doğruluk Testi — 30 etiketlenmiş geri bildirim")
print("Dağılım: 10 Pozitif / 10 Negatif / 10 Nötr")
print("=" * 60)

correct = 0
results = []

for tc in TEST_CASES:
    raw = classify(tc["text"])
    match = tc["expected"].lower() in raw.lower()
    correct += int(match)
    status = "✅" if match else "❌"
    results.append({
        "id": tc["id"],
        "expected": tc["expected"],
        "got": raw,
        "match": match
    })
    print(f"{status}  [{tc['id']:2}] Beklenen: {tc['expected']:<9} GPT: {raw}")
    print(f"       Metin: {tc['text'][:70]}")
    print()

accuracy = correct / len(TEST_CASES) * 100

print("=" * 60)
print(f"DOĞRULUK: {correct}/{len(TEST_CASES)} = %{accuracy:.0f}")
if accuracy >= 90:
    print("SONUÇ: ✅ IP-5 başarı ölçütü KARŞILANDI (%90 eşiği aşıldı)")
else:
    print(f"SONUÇ: ⚠️  IP-5 başarı ölçütü karşılanmadı (hedef %90, elde %{accuracy:.0f})")
print("=" * 60)

report = {
    "test_date": "2026-05-02",
    "model": MODEL,
    "n": len(TEST_CASES),
    "correct": correct,
    "accuracy_pct": round(accuracy, 1),
    "threshold_pct": 90,
    "passed": accuracy >= 90,
    "label_distribution": {"Pozitif": 10, "Negatif": 10, "Nötr": 10},
    "results": results,
}

with open("gpt_accuracy_report.json", "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)

print("Rapor kaydedildi: gpt_accuracy_report.json")