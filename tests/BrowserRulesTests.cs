using NiyazisugarItemci;
void Check(bool condition, string description) { if (!condition) throw new Exception(description); }
Check(BrowserTabRules.Family("https://itemci.com/panel/mesajlar/123") == "messages", "Conversation routes must share the message tab");
Check(BrowserTabRules.Family("https://itemci.com/panel/mesaj-kutusu") == "messages", "Message inbox alias");
Check(BrowserTabRules.IsMessageLanding("https://itemci.com/panel/mesajlar"), "Inbox clicks must preserve conversation");
Check(!BrowserTabRules.IsMessageLanding("https://itemci.com/panel/mesajlar?conversation=123"), "Conversation link must navigate");
Check(!BrowserTabRules.IsMessageLanding("https://itemci.com/panel/mesajlar/123"), "Conversation path must navigate");
Check(!BrowserTabRules.SamePage("https://itemci.com/panel/mesajlar?id=1", "https://itemci.com/panel/mesajlar?id=2"), "Distinct conversations cannot collapse by ignoring queries");
Check(BrowserTabRules.SamePage("https://itemci.com/panel/ilanlarim?page=1&status=4", "https://itemci.com/panel/ilanlarim/?status=4&page=1"), "Query ordering and trailing slash should not create duplicates");
Check(BrowserTabRules.Family("https://itemci.com/panel/ilan-ekle") == "create", "Listing editor tab reuse");
Check(BrowserTabRules.Family("https://evilitemci.com/panel/mesajlar") == "", "Only real Itemci hosts share a tab role");
Console.WriteLine("PASS: inbox/conversation reuse, listing editor routes, distinct query identities, host checks.");
