# Niyazisugar Itemci Updates

Bu repository **Niyazisugar Itemci** masaüstü uygulamasının otomatik güncelleme kanalıdır.

Uygulama şu sabit dosyayı kontrol eder:

`https://github.com/niyazisugar/NiyazisugarItemci-Updates/releases/latest/download/latest.json`

## Güncelleme akışı

1. `source-parts/*.b64` içinde güncel uygulama kaynak paketinin parçaları tutulur.
2. `publish.json` yayınlanacak sürüm numarası ve sürüm notlarını taşır.
3. `publish.json` değiştiğinde GitHub Actions Windows üzerinde kaynak ZIP'ini yeniden oluşturur.
4. Ana uygulama ve `NiyazisugarUpdater.exe` derlenir.
5. SHA-256 doğrulamalı `NiyazisugarItemci-update.zip` ve `latest.json` hazırlanır.
6. Dosyalar GitHub Release olarak yayınlanır.
7. Kurulu uygulama yeni sürümü görür ve tek tıkla indirip günceller.

## Yeni sürüm yayınlama sırası

Önce kaynak paket parçaları güncellenir.  
**En son** `publish.json` içindeki `version` artırılır ve sürüm notları değiştirilir.

> Bu repo herkese açık olduğu için kaynak paket parçaları da erişilebilir durumdadır. Uygulama kaynaklarında şifre, token veya kullanıcı oturum bilgisi tutulmamalıdır.
