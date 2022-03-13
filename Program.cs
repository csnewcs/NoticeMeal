using System.Net;

using Discord;
using Discord.WebSocket;
using Discord.Commands;

using Newtonsoft.Json.Linq;

namespace NoticeMeal
{
    class Program {
        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        DiscordSocketClient client;
        JObject setting;
        JObject sendGuildsAndChannels;
        public async Task MainAsync() {
            client = new DiscordSocketClient();
            setting = getSetting();
            sendGuildsAndChannels = listingToSend();
            clientEventSet();
            await client.LoginAsync(TokenType.Bot, setting["token"].ToString());
            await client.StartAsync();
            await Task.Delay(-1);
        }
        private JObject getSetting() {
            JObject setting = new JObject();
            try {
                setting = JObject.Parse(File.ReadAllText("config.json"));
            }
            catch {
                makeSetting();
                setting = getSetting();
            }
            return setting;
        }
        private void makeSetting() {
            JObject setting = new JObject();
            Console.WriteLine("설정 파일이 없거나 잘못되어 초기 설정을 시작합니다.\n봇의 디스코드 토큰을 입력하세요.");
            setting.Add("token", Console.ReadLine());
            setting.Add("isTesting", false);
            setting.Add("testGuild", 0);
            File.WriteAllText("config.json", setting.ToString());
        }
        private JObject listingToSend() {
            const string path = "./send.json";
            JObject open = new JObject();
            try {
                open = JObject.Parse(File.ReadAllText(path));
            }
            catch {
                File.WriteAllText(path, "{}");
            }
            return open;
        }
        private void clientEventSet() {
            client.Ready += readyAsync;
            client.Log += log;
            client.SlashCommandExecuted += slashCommandExecutedAsync;
        }
        private async Task readyAsync() {
            await makeCommands();
        }
        private async Task makeCommands() {
            SlashCommandBuilder setChannel = new SlashCommandBuilder().WithName("채널설정").WithDescription("급식 알림이 올 채널을 여기로 설정합니다.(다른 채널에 가던 알림은 사라집니다.)").AddOption("학교", ApplicationCommandOptionType.String, "학교 이름을 알려주세요.");
            if((bool)setting["isTesting"]) {
                SocketGuild testGuild = client.GetGuild((ulong)setting["testGuild"]);
                await testGuild.DeleteApplicationCommandsAsync();
                await testGuild.CreateApplicationCommandAsync(setChannel.Build());
            }
            else {
                await client.CreateGlobalApplicationCommandAsync(setChannel.Build());
            }
        }
        private Task log(LogMessage log) {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }
        private async Task slashCommandExecutedAsync(SocketSlashCommand command) {
            switch(command.CommandName) {
                case "채널설정":
                    await command.RespondAsync("잠시 기다려 주세요");
                    addChannel(((SocketGuildUser)command.User).Guild.Id, command.GetChannelAsync().Result.Id, command.Data.Options.First().ToString());
                    await command.ModifyOriginalResponseAsync(m => {m.Content = "설정이 완료되었습니다.";});
                    break;
            }
        }
        private void addChannel(ulong guildID, ulong channelID, string schoolName) {
            JObject values = new JObject();
            values.Add("channel", channelID);
            values.Add("school", schoolName);
            if(sendGuildsAndChannels.ContainsKey(guildID.ToString())) {
                sendGuildsAndChannels[guildID] = values;
            }
            else {
                sendGuildsAndChannels.Add(guildID.ToString(), values);
            }
            writeToJson();
        }
        private void writeToJson() {
            const string path = "./send.json";
            File.WriteAllText(path, sendGuildsAndChannels.ToString());
        }
        
        private async Task send() {
            DateTime dt = DateTime.Now.ToUniversalTime();
            dt = dt.AddHours(9);
            bool sended = false;
            if(dt.Hour == 0 && dt.Minute == 1 && !sended) {
                foreach(var guild in sendGuildsAndChannels) {
                    string[] meal = getMeal(guild.Value["school"].ToString());
                    EmbedBuilder builder = new EmbedBuilder().AddField("아침", meal[0]).AddField("점심", meal[1]).AddField("저녁", meal[2]);
                    await client.GetGuild(ulong.Parse(guild.Key)).GetTextChannel((ulong)guild.Value["channel"]).SendMessageAsync("", embed: builder.Build());
                }
            }
            else sended = false;
        }
        private string[] getMeal(string schoolName) {
            const string baseUrl = "https://scmeal.ml/";
            string[] meals = new string[3];
            HttpClient client = new HttpClient();
            string download = client.GetStringAsync(baseUrl + schoolName).Result;
            JObject result = JObject.Parse(download);
            JArray[] menu = new JArray[] {result["breakfast"] as JArray, result["lunch"] as JArray, result["dinner"] as JArray};

            for(int i = 0; i < 3; i++) {
                foreach(var m in menu[i]) {
                    meals[i] += m.ToString() + "\n";
                }
            }
            return meals;
        }
    }
}
