namespace NiyazisugarItemci;
internal sealed class AutoBumpRecord
{
    public DateTime nextAllowedUtc{get;set;}
    public DateTime lastAttemptUtc{get;set;}
    public DateTime lastSuccessUtc{get;set;}
    public string status{get;set;}="";
}
internal static class AutoBumpSchedule
{
    internal static DateTime GlobalNextAllowed(IEnumerable<AutoBumpRecord> records,DateTime savedGate) => records.Select(r=>r.lastAttemptUtc.AddSeconds(90)).Append(savedGate).Max();
    internal static DateTime AfterAttempt(DateTime completedAt,bool rateLimited,int retryAfterSeconds) => completedAt.AddSeconds(rateLimited?Math.Max(300,Math.Min(86400,retryAfterSeconds)):Math.Max(90,Math.Min(86400,retryAfterSeconds)));
    internal static bool CanAttempt(AutoBumpRecord? record,DateTime now)=>record is null||record.nextAllowedUtc<=now;
    internal static void Reserve(AutoBumpRecord record,DateTime now)
    {
        record.lastAttemptUtc=now;record.nextAllowedUtc=now.AddHours(1);record.status="Gönderim sonucu bekleniyor";
    }
    internal static void Complete(AutoBumpRecord record,DateTime now,bool ok,bool uncertain,bool notSent,int retryAfterSeconds)
    {
        if(ok){record.lastSuccessUtc=now;record.nextAllowedUtc=now.AddHours(1);record.status="Ücretsiz yukarı taşındı";}
        else if(uncertain){record.status="Sonuç belirsiz; aynı saat içinde tekrar gönderilmeyecek";}
        else {record.nextAllowedUtc=now.AddSeconds(Math.Max(notSent?60:300,Math.Min(86400,retryAfterSeconds)));record.status=notSent?"Site düğmesi henüz aktif değil":"Site işlemi kabul etmedi";}
    }
    internal static DateTime NextScan(IEnumerable<AutoBumpRecord> records,DateTime now,DateTime discoverAt)
    {
        var earliest=records.Select(r=>r.nextAllowedUtc).DefaultIfEmpty(discoverAt).Min();
        return new[]{earliest,discoverAt}.Min()<now.AddMinutes(1)?now.AddMinutes(1):new[]{earliest,discoverAt}.Min();
    }
}
