# Niyazisugar Itemci Updates

Bu repository **Niyazisugar Itemci** masaüstü uygulamasının otomatik güncelleme kanalıdır.

Uygulama şu sabit dosyayı kontrol eder:

`https://github.com/niyazisugar/NiyazisugarItemci-Updates/releases/latest/download/latest.json`

## Nasıl çalışır?

1. `source-current.zip` içinde güncel uygulama kaynak paketi bulunur.
2. `publish.json` yayınlanacak sürüm numarası ve sürüm notlarını taşır.
3. `publish.json` değiştiğinde GitHub Actions Windows üzerinde uygulamayı derler.
4. Ana uygulama ve `NiyazisugarUpdater.exe` hazırlanır.
5. SHA-256 doğrulamalı `NiyazisugarItemci-update.zip` ve `latest.json` oluşturulur.
6. Dosyalar GitHub Release olarak yayınlanır.
7. Kurulu uygulama yeni sürümü görür ve tek tıkla indirip günceller.

## Yeni sürüm yayınlama

Önce `source-current.zip` güncellenir.  
En son `publish.json` içindeki `version` artırılır ve sürüm notları değiştirilir.

> Bu repo herkese açık olduğu için kaynak paketi de erişilebilir durumdadır. Uygulama kaynaklarında şifre, token veya kullanıcı oturum bilgisi tutulmamalıdır.
