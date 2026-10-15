using System.Text.Json;
using NiyazisugarItemci;
void Check(bool ok,string message){if(!ok)throw new Exception(message);}
var now=new DateTime(2026,10,14,7,0,0,DateTimeKind.Utc);
var record=new AutoBumpRecord();
Check(AutoBumpSchedule.CanAttempt(record,now),"New listing should be eligible for a site eligibility check.");
AutoBumpSchedule.Reserve(record,now);
var restored=JsonSerializer.Deserialize<AutoBumpRecord>(JsonSerializer.Serialize(record))!;
Check(!AutoBumpSchedule.CanAttempt(restored,now.AddMinutes(59)),"Pending reservation must survive restart and prevent duplicate clicks.");
AutoBumpSchedule.Complete(restored,now.AddSeconds(1),true,false,false,0);
Check(!AutoBumpSchedule.CanAttempt(restored,now.AddHours(1)),"Success must hold for a full hour after confirmation.");
Check(AutoBumpSchedule.CanAttempt(restored,now.AddHours(1).AddSeconds(1)),"Hour boundary must unlock the next eligibility check.");
AutoBumpSchedule.Reserve(record,now);AutoBumpSchedule.Complete(record,now.AddSeconds(1),false,true,false,0);
Check(record.nextAllowedUtc==now.AddHours(1),"Uncertain result may not immediately retry.");
AutoBumpSchedule.Complete(record,now,false,false,false,7200);
Check(record.nextAllowedUtc==now.AddHours(2),"Longer platform wait must be honored.");
Check(AutoBumpSchedule.NextScan(new[]{restored},now,now.AddMinutes(10))==now.AddMinutes(10),"Idle tracking should avoid constant page reloads while waiting.");
Check(AutoBumpSchedule.NextScan(new[]{new AutoBumpRecord{nextAllowedUtc=now}},now,now.AddMinutes(10))==now.AddMinutes(1),"Due actions should check within the next minute.");
Check(AutoBumpSchedule.NextScan(Array.Empty<AutoBumpRecord>(),now,now.AddMinutes(10))==now.AddMinutes(10),"Sold/inactive listings must not cause a permanent minute-by-minute reload loop.");
Console.WriteLine("PASS: hour boundary, durable pre-click reservation, unknown outcomes, platform limits and bounded idle polling.");

var gate=AutoBumpSchedule.AfterAttempt(now,false,0);
Check(gate==now.AddSeconds(90),"Different listings must be spaced by at least 90 seconds.");
Check(AutoBumpSchedule.GlobalNextAllowed(new[]{record},DateTime.MinValue)==record.lastAttemptUtc.AddSeconds(90),"Migration must honor the last persisted attempt across listings.");
Check(AutoBumpSchedule.GlobalNextAllowed(Array.Empty<AutoBumpRecord>(),gate)==gate,"Global cooldown must survive restart independently of listing cleanup.");
Check(AutoBumpSchedule.AfterAttempt(now,true,0)==now.AddMinutes(5),"Rate limit must stop the whole queue for at least five minutes.");
Check(AutoBumpSchedule.AfterAttempt(now,true,7200)==now.AddHours(2),"Platform retry-after must delay the entire queue.");
Console.WriteLine("PASS: global 90-second spacing, restart migration, throttle backoff and platform Retry-After.");
