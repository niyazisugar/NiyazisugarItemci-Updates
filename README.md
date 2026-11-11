# NiyaziSUGAR Itemci

itemci.com satıcıları için Windows masaüstü uygulaması. Bu repo hem uygulamanın kaynak kodunu hem de otomatik güncelleme kanalını taşır.

## Klasörler

| Klasör | İçerik |
| --- | --- |
| `src/NiyazisugarItemci/App` | Ana pencere, ayarlar, bildirimler, sistem tepsisi |
| `src/NiyazisugarItemci/Design` | Renkler, yazı tipleri, ortak bileşenler, üst menü |
| `src/NiyazisugarItemci/Pages` | Sayfalar: tarayıcı, ilanlar, satışlar, piyasa, takip, hızlı ilan |
| `src/NiyazisugarItemci/Services` | Güncelleme, reklam engelleme, sekme kuralları, otomatik öne çıkarma |
| `src/NiyazisugarItemci/Web` | Site içinde çalışan betikler (piyasa analizi, ilan formu vb.) |
| `src/NiyazisugarUpdater` | Güncellemeyi kuran yardımcı program |
| `tests` | Tarayıcı ve Windows testleri |

## Yeni sürüm yayınlama

1. Değişiklik bir pull request ile gelir; derleme, testler ve ekran görüntüleri otomatik çalışır, sürüm yayınlanmaz.
2. Pull request birleştirilir.
3. `publish.json` içindeki `version` artırılır ve sürüm notu yazılır.
4. GitHub Actions uygulamayı derler, SHA-256 doğrulamalı paketi ve `latest.json` dosyasını GitHub Release olarak yayınlar.
5. Kurulu uygulama şu dosyayı kontrol eder ve yeni sürümü tek tıkla kurar:
   `https://github.com/niyazisugar/NiyazisugarItemci-Updates/releases/latest/download/latest.json`

Sürüm numarası yalnızca `publish.json` içinde yazılır; derleme sırasında uygulamaya aktarılır.

> Bu repo herkese açıktır. Kaynaklarda şifre, token veya kullanıcı oturum bilgisi tutulmamalıdır.
