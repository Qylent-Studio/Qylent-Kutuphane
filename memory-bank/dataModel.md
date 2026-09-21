# Veri Modeli Kararları

Temel kayıtlar: `LibraryProfile`, `Operator`, `Member`, `MemberFieldDefinition`, `MemberFieldValue`, `BookTitle`, `BookCopy`, `Loan`, `Reservation`, `LoanRule`, `ReportPreset`, `AuditEntry`, `BackupRecord`.

- Kimlikler GUID olur.
- Barkod/demirbaş kodu benzersizdir.
- Aktif ödünçte bir fiziksel kopya yalnızca bir üyede olabilir.
- Bir kitap başlığı için ayırtmalar istek zamanı sırasıyla işlenir.
- Tarihler UTC tutulur; rapor aralıkları yerel takvim günlerinden UTC sınırlarına çevrilir.

