using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using LiteDB;
using System.Linq;

// ========== МОДЕЛИ БД ==========
public class BotUser
{
    [BsonId]
    public long UserId { get; set; }
    public string Username { get; set; }
    public List<string> Codes { get; set; } = new List<string>();
    public DateTime CreatedAt { get; set; }
    public bool ShowSenderUsername { get; set; } = false;
}

public class BotMessage
{
    [BsonId(true)]
    public int MessageId { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public string SenderUsername { get; set; }
    public string MessageText { get; set; }
    public string FileId { get; set; }
    public string MessageType { get; set; }
    public DateTime SentAt { get; set; }
}

public class AnonymousLink
{
    [BsonId(true)]
    public int LinkId { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ========== ОСНОВНАЯ ПРОГРАММА ==========
class Program
{
    private static TelegramBotClient botClient;
    private static string botToken = "! ! ! ! ВАШ ТОКЕН ! ! ! !";
    private static string dbPath = "bot_database.db";
    private static Random random = new Random();
    private const int MAX_CODES_PER_USER = 1;
    private const long ADMIN_ID = 973301626;

    static void Main(string[] args)
    {
        Console.WriteLine("════════════════════════════════════════");
        Console.WriteLine("🤖 ЗАПУСК БОТА АНОНИМНЫХ ВОПРОСОВ");
        Console.WriteLine("════════════════════════════════════════\n");

        string fullPath = System.IO.Path.GetFullPath(dbPath);
        string directory = System.IO.Path.GetDirectoryName(fullPath);

        Console.WriteLine($"📂 Рабочая директория: {System.IO.Directory.GetCurrentDirectory()}");
        Console.WriteLine($"📂 Полный путь БД: {fullPath}");
        Console.WriteLine($"📂 Директория существует: {System.IO.Directory.Exists(directory)}\n");

        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
            Console.WriteLine($"✅ Создана директория: {directory}\n");
        }

        botClient = new TelegramBotClient(botToken);

        try
        {
            var me = botClient.GetMeAsync().Result;
            Console.WriteLine($"✅ Бот успешно подключён!");
            Console.WriteLine($"👤 Имя бота: {me.FirstName}");
            Console.WriteLine($"🔖 Username: @{me.Username}");
            Console.WriteLine($"📍 ID: {me.Id}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка подключения к боту: {ex.Message}");
            Console.WriteLine($"⚠️ Проверьте корректность TOKEN");
            Console.WriteLine($"ℹ️ Получить новый TOKEN можно у @BotFather в Telegram\n");
            return;
        }

        Console.WriteLine("════════════════════════════════════════");
        Console.WriteLine("📊 ИНИЦИАЛИЗАЦИЯ БАЗЫ ДАННЫХ");
        Console.WriteLine("════════════════════════════════════════\n");

        try
        {
            InitializeDatabase();

            using (var db = new LiteDatabase(dbPath))
            {
                int usersCount = db.GetCollection<BotUser>("users").Count();
                int messagesCount = db.GetCollection<BotMessage>("messages").Count();
                int linksCount = db.GetCollection<AnonymousLink>("anonymous_links").Count();

                Console.WriteLine($"✅ База данных инициализирована успешно!");
                Console.WriteLine($"📍 Расположение: {fullPath}");
                Console.WriteLine($"📊 Текущие данные:");
                Console.WriteLine($"   • Пользователей: {usersCount}");
                Console.WriteLine($"   • Сообщений: {messagesCount}");
                Console.WriteLine($"   • Анонимных ссылок: {linksCount}\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка инициализации БД: {ex.Message}");
            Console.WriteLine($"⚠️ Попробуйте удалить файл БД и пересоберите проект\n");
            return;
        }

        Console.WriteLine("════════════════════════════════════════");
        Console.WriteLine("🚀 ЗАПУСК POLLING");
        Console.WriteLine("════════════════════════════════════════\n");

        using (var cts = new CancellationTokenSource())
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cts.Token
            );

            Console.WriteLine("✅ Бот успешно запущен и ожидает сообщений!");
            Console.WriteLine("💬 Доступные команды:");
            Console.WriteLine("   • /start   - Получить анонимную ссылку");
            Console.WriteLine("   • /secret  - Включить/отключить показ отправителя");
            Console.WriteLine("   • /stats   - Показать статистику БД (только админ)");
            Console.WriteLine("   • /testdb  - Проверить БД (только админ)");
            Console.WriteLine("   • /cleardb - Очистить БД (только админ)");
            Console.WriteLine("\n════════════════════════════════════════");
            Console.WriteLine("🔴 Нажмите Enter для остановки бота");
            Console.WriteLine("════════════════════════════════════════\n");

            Console.ReadLine();

            Console.WriteLine("\n⏹️ Остановка бота...");
            cts.Cancel();
        }

        Console.WriteLine("✅ Бот остановлен\n");
    }

    private static void InitializeDatabase()
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var usersCollection = db.GetCollection<BotUser>("users");
                var messagesCollection = db.GetCollection<BotMessage>("messages");
                var linksCollection = db.GetCollection<AnonymousLink>("anonymous_links");

                usersCollection.EnsureIndex(u => u.UserId);
                messagesCollection.EnsureIndex(m => m.ReceiverId);
                messagesCollection.EnsureIndex(m => m.SenderId);
                linksCollection.EnsureIndex(l => l.SenderId);
                linksCollection.EnsureIndex(l => l.ReceiverId);

                Console.WriteLine("✅ Таблицы созданы успешно");
                Console.WriteLine("✅ Индексы созданы успешно");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            throw;
        }
    }

    private static Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.CallbackQuery)
        {
            return HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
        }

        if (update.Type == UpdateType.Message)
        {
            return HandleMessageAsync(botClient, update.Message, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private static Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        long userId = callbackQuery.From.Id;
        string username = callbackQuery.From.Username ?? "unknown";

        Console.WriteLine($"🔘 Нажата кнопка от @{username}: {callbackQuery.Data}");

        if (callbackQuery.Data == "write_more")
        {
            var link = GetAnonymousLinkForUser(userId);

            if (link != null)
            {
                botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "🚀 Здесь можно отправить анонимное сообщение человеку, который опубликовал эту ссылку\n\n" +
                          "🖊 Напишите сюда всё, что хотите ему передать, и через несколько секунд он получит ваше сообщение, " +
                          "но не будет знать от кого\n\n" +
                          "Отправить можно фото, видео, 💬 текст, 🔊 голосовые, 📷 видеосообщения (кружки), а также ✨ стикеры",
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"✅ Отправлено приветствие пользователю {userId}");
            }
            else
            {
                botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "❌ Сессия истекла. Откройте ссылку заново.",
                    cancellationToken: cancellationToken
                );
            }
        }

        botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

        return Task.CompletedTask;
    }

    private static Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        long userId = message.From.Id;
        string username = message.From.Username ?? "unknown";

        if (message.Text != null)
        {
            Console.WriteLine($"📨 Текст от @{username} ({userId}): {message.Text}");
            return HandleTextMessageAsync(botClient, message, userId, username, cancellationToken);
        }
        else if (message.Photo != null)
        {
            Console.WriteLine($"📷 Фото от @{username} ({userId})");
            return HandlePhotoAsync(botClient, message, userId, username, cancellationToken);
        }
        else if (message.Video != null)
        {
            Console.WriteLine($"🎬 Видео от @{username} ({userId})");
            return HandleVideoAsync(botClient, message, userId, username, cancellationToken);
        }
        else if (message.Voice != null)
        {
            Console.WriteLine($"🔊 Голос от @{username} ({userId})");
            return HandleVoiceAsync(botClient, message, userId, username, cancellationToken);
        }
        else if (message.VideoNote != null)
        {
            Console.WriteLine($"🎞️ Видео сообщение от @{username} ({userId})");
            return HandleVideoNoteAsync(botClient, message, userId, username, cancellationToken);
        }
        else if (message.Sticker != null)
        {
            Console.WriteLine($"✨ Стикер от @{username} ({userId})");
            return HandleStickerAsync(botClient, message, userId, username, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private static Task HandleTextMessageAsync(ITelegramBotClient botClient, Message message, long userId, string username, CancellationToken cancellationToken)
    {
        string messageText = message.Text;

        if (messageText == "/start")
        {
            var existingUser = GetUserByUserId(userId);

            if (existingUser == null)
            {
                List<string> codes = new List<string>();
                for (int i = 0; i < MAX_CODES_PER_USER; i++)
                {
                    codes.Add(GenerateUniqueCode());
                }

                SaveUserWithCodes(userId, username, codes);

                string msg = "Начните получать анонимные вопросы прямо сейчас!\n\n";
                string shareLink = $"<https://t.me/questanss_bot?start={codes[0]}";
                msg += $"👉 [{shareLink}]({shareLink})\n\n";
                msg += "Разместите эту ссылку ☝️ в описании своего профиля Telegram, TikTok, Instagram (stories), " +
                       "чтобы вам могли написать 💬\n\n";

                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: msg,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"✅ Создан новый пользователь: @{username}");
            }
            else
            {
                string msg = "Начните получать анонимные вопросы прямо сейчас!\n\n";
                string shareLink = $"<https://t.me/questanss_bot?start={existingUser.Codes[0]}";
                msg += $"👉 [{shareLink}]({shareLink})\n\n";
                msg += "Разместите эту ссылку ☝️ в описании своего профиля Telegram, TikTok, Instagram (stories), " +
                       "чтобы вам могли написать 💬\n\n";

                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: msg,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"✅ Вернулся пользователь: @{username}");
            }
        }

        else if (messageText == "/secret")
        {
            var user = GetUserByUserId(userId);

            if (user == null)
            {
                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "❌ Вы не зарегистрированы. Напишите /start",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                bool newMode = !user.ShowSenderUsername;
                UpdateUserShowSenderUsername(userId, newMode);

                string status = newMode ? "🔓 ВКЛЮЧЕНО" : "🔒 ОТКЛЮЧЕНО";
                string description = newMode
                    ? "Теперь вы видите от кого приходят сообщения (@username)"
                    : "Теперь все сообщения полностью анонимные";

                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"🔐 Режим приватности\n\n{status}\n\n{description}",
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"🔐 Пользователь @{username} переключил режим на: {newMode}");
            }
        }

        else if (messageText.StartsWith("/start "))
        {
            string code = messageText.Substring(7).Trim();
            var owner = GetUserByCode(code);

            if (owner != null)
            {
                SaveAnonymousLink(userId, owner.UserId);

                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "🚀 Здесь можно отправить анонимное сообщение человеку, который опубликовал эту ссылку\n\n" +
                          "🖊 Напишите сюда всё, что хотите ему передать, и через несколько секунд он получит ваше сообщение, " +
                          "но не будет знать от кого\n\n" +
                          "Отправить можно фото, видео, 💬 текст, 🔊 голосовые, 📷 видеосообщения (кружки), а также ✨ стикеры",
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"✅ Пользователь {userId} получил доступ к ссылке владельца {owner.UserId}");
            }
            else
            {
                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "❌ Ссылка истекла или не найдена",
                    cancellationToken: cancellationToken
                );
            }
        }

        // ====== АДМИН-КОМАНДЫ ======

        else if (messageText == "/stats")
        {
            if (userId != ADMIN_ID)
            {
                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "❌ Эта команда доступна только администратору.",
                    cancellationToken: cancellationToken
                );
                return Task.CompletedTask;
            }

            try
            {
                using (var db = new LiteDatabase(dbPath))
                {
                    int usersCount = db.GetCollection<BotUser>("users").Count();
                    int messagesCount = db.GetCollection<BotMessage>("messages").Count();
                    int linksCount = db.GetCollection<AnonymousLink>("anonymous_links").Count();

                    botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"📊 **Статистика БД:**\n\n" +
                              $"👥 Пользователей: {usersCount}\n" +
                              $"💬 Сообщений: {messagesCount}\n" +
                              $"🔗 Анонимных ссылок: {linksCount}",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }

        else if (messageText == "/testdb")
        {
            if (userId != ADMIN_ID)
            {
                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "❌ Эта команда доступна только администратору.",
                    cancellationToken: cancellationToken
                );
                return Task.CompletedTask;
            }

            try
            {
                using (var db = new LiteDatabase(dbPath))
                {
                    int count = db.GetCollection<BotUser>("users").Count();

                    botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"✅ БД работает корректно!\n\nПользователей: {count}",
                        cancellationToken: cancellationToken
                    );

                    Console.WriteLine($"✅ БД активна, пользователей: {count}");
                }
            }
            catch (Exception ex)
            {
                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"❌ Ошибка БД: {ex.Message}",
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }

        else if (messageText == "/cleardb")
        {
            if (userId != ADMIN_ID)
            {
                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "❌ Эта команда доступна только администратору.",
                    cancellationToken: cancellationToken
                );
                return Task.CompletedTask;
            }

            try
            {
                using (var db = new LiteDatabase(dbPath))
                {
                    db.GetCollection<BotUser>("users").DeleteAll();
                    db.GetCollection<BotMessage>("messages").DeleteAll();
                    db.GetCollection<AnonymousLink>("anonymous_links").DeleteAll();
                }

                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "🗑️ База данных полностью очищена!",
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"🗑️ БД очищена пользователем @{username}");
            }
            catch (Exception ex)
            {
                botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"❌ Ошибка очистки: {ex.Message}",
                    cancellationToken: cancellationToken
                );
            }
        }

        // ====== КОНЕЦ АДМИН-КОМАНД ======

        else
        {
            SendAnonymousMessage(botClient, userId, username, messageText, null, "text", message.Chat.Id, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private static Task HandlePhotoAsync(ITelegramBotClient botClient, Message message, long userId, string username, CancellationToken cancellationToken)
    {
        var link = GetAnonymousLinkForUser(userId);

        if (link != null)
        {
            long ownerId = link.ReceiverId;
            string fileId = message.Photo[message.Photo.Length - 1].FileId;
            string caption = message.Caption ?? "[Фото без подписи]";

            var owner = GetUserByUserId(ownerId);
            string photoCaption = GetOwnerMessage(owner, username, caption);

            botClient.SendPhotoAsync(
                chatId: ownerId,
                photo: new InputFileId(fileId),
                caption: photoCaption,
                cancellationToken: cancellationToken
            );

            if (owner != null && owner.ShowSenderUsername)
            {
                botClient.SendPhotoAsync(
                    chatId: ownerId,
                    photo: new InputFileId(fileId),
                    caption: $"📷 Анонимное фото:\n\n{caption}",
                    cancellationToken: cancellationToken
                );
            }

            SendConfirmation(botClient, message.Chat.Id, cancellationToken);
            SaveMessage(userId, ownerId, username, caption, fileId, "photo");

            Console.WriteLine($"📷 Фото отправлено владельцу {ownerId}");
        }
        else
        {
            botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "❌ Сначала откройте бота через правильную ссылку",
                cancellationToken: cancellationToken
            );
        }

        return Task.CompletedTask;
    }

    private static Task HandleVideoAsync(ITelegramBotClient botClient, Message message, long userId, string username, CancellationToken cancellationToken)
    {
        var link = GetAnonymousLinkForUser(userId);

        if (link != null)
        {
            long ownerId = link.ReceiverId;
            string fileId = message.Video.FileId;
            string caption = message.Caption ?? "[Видео без подписи]";

            var owner = GetUserByUserId(ownerId);
            string videoCaption = GetOwnerMessage(owner, username, caption);

            botClient.SendVideoAsync(
                chatId: ownerId,
                video: new InputFileId(fileId),
                caption: videoCaption,
                cancellationToken: cancellationToken
            );

            if (owner != null && owner.ShowSenderUsername)
            {
                botClient.SendVideoAsync(
                    chatId: ownerId,
                    video: new InputFileId(fileId),
                    caption: $"🎬 Анонимное видео:\n\n{caption}",
                    cancellationToken: cancellationToken
                );
            }

            SendConfirmation(botClient, message.Chat.Id, cancellationToken);
            SaveMessage(userId, ownerId, username, caption, fileId, "video");

            Console.WriteLine($"🎬 Видео отправлено владельцу {ownerId}");
        }
        else
        {
            botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "❌ Сначала откройте бота через правильную ссылку",
                cancellationToken: cancellationToken
            );
        }

        return Task.CompletedTask;
    }

    private static Task HandleVoiceAsync(ITelegramBotClient botClient, Message message, long userId, string username, CancellationToken cancellationToken)
    {
        var link = GetAnonymousLinkForUser(userId);

        if (link != null)
        {
            long ownerId = link.ReceiverId;
            string fileId = message.Voice.FileId;

            var owner = GetUserByUserId(ownerId);

            botClient.SendVoiceAsync(
                chatId: ownerId,
                voice: new InputFileId(fileId),
                caption: owner != null && owner.ShowSenderUsername ? $"🔊 Аудиосообщение от @{username}" : "🔊 Анонимное аудиосообщение",
                cancellationToken: cancellationToken
            );

            if (owner != null && owner.ShowSenderUsername)
            {
                botClient.SendVoiceAsync(
                    chatId: ownerId,
                    voice: new InputFileId(fileId),
                    caption: "🔊 Анонимное аудиосообщение",
                    cancellationToken: cancellationToken
                );
            }

            SendConfirmation(botClient, message.Chat.Id, cancellationToken);
            SaveMessage(userId, ownerId, username, "[Голосовое сообщение]", fileId, "voice");

            Console.WriteLine($"🔊 Голос отправлен владельцу {ownerId}");
        }
        else
        {
            botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "❌ Сначала откройте бота через правильную ссылку",
                cancellationToken: cancellationToken
            );
        }

        return Task.CompletedTask;
    }

    private static Task HandleVideoNoteAsync(ITelegramBotClient botClient, Message message, long userId, string username, CancellationToken cancellationToken)
    {
        var link = GetAnonymousLinkForUser(userId);

        if (link != null)
        {
            long ownerId = link.ReceiverId;
            string fileId = message.VideoNote.FileId;

            var owner = GetUserByUserId(ownerId);

            botClient.SendVideoNoteAsync(
                chatId: ownerId,
                videoNote: new InputFileId(fileId),
                cancellationToken: cancellationToken
            );

            if (owner != null && owner.ShowSenderUsername)
            {
                botClient.SendTextMessageAsync(
                    chatId: ownerId,
                    text: $"📹 Видеосообщение от @{username}",
                    cancellationToken: cancellationToken
                );

                botClient.SendVideoNoteAsync(
                    chatId: ownerId,
                    videoNote: new InputFileId(fileId),
                    cancellationToken: cancellationToken
                );

                botClient.SendTextMessageAsync(
                    chatId: ownerId,
                    text: "📹 Анонимное видеосообщение",
                    cancellationToken: cancellationToken
                );
            }

            SendConfirmation(botClient, message.Chat.Id, cancellationToken);
            SaveMessage(userId, ownerId, username, "[Видеосообщение]", fileId, "video_note");

            Console.WriteLine($"📹 Видеосообщение отправлено владельцу {ownerId}");
        }
        else
        {
            botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "❌ Сначала откройте бота через правильную ссылку",
                cancellationToken: cancellationToken
            );
        }

        return Task.CompletedTask;
    }

    private static Task HandleStickerAsync(ITelegramBotClient botClient, Message message, long userId, string username, CancellationToken cancellationToken)
    {
        var link = GetAnonymousLinkForUser(userId);

        if (link != null)
        {
            long ownerId = link.ReceiverId;
            string fileId = message.Sticker.FileId;

            var owner = GetUserByUserId(ownerId);

            botClient.SendStickerAsync(
                chatId: ownerId,
                sticker: new InputFileId(fileId),
                cancellationToken: cancellationToken
            );

            if (owner != null && owner.ShowSenderUsername)
            {
                botClient.SendTextMessageAsync(
                    chatId: ownerId,
                    text: $"✨ Стикер от @{username}",
                    cancellationToken: cancellationToken
                );

                botClient.SendStickerAsync(
                    chatId: ownerId,
                    sticker: new InputFileId(fileId),
                    cancellationToken: cancellationToken
                );

                botClient.SendTextMessageAsync(
                    chatId: ownerId,
                    text: "✨ Анонимный стикер",
                    cancellationToken: cancellationToken
                );
            }

            SendConfirmation(botClient, message.Chat.Id, cancellationToken);
            SaveMessage(userId, ownerId, username, "[Стикер]", fileId, "sticker");

            Console.WriteLine($"✨ Стикер отправлен владельцу {ownerId}");
        }
        else
        {
            botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "❌ Сначала откройте бота через правильную ссылку",
                cancellationToken: cancellationToken
            );
        }

        return Task.CompletedTask;
    }

    private static string GetOwnerMessage(BotUser owner, string username, string content)
    {
        if (owner != null && owner.ShowSenderUsername)
        {
            return $"📩 Анонимное сообщение от @{username}:\n\n{content}";
        }
        return $"📩 Анонимное сообщение:\n\n{content}";
    }

    private static void SendAnonymousMessage(ITelegramBotClient botClient, long userId, string username, string messageText, string fileId, string messageType, long chatId, CancellationToken cancellationToken)
    {
        var link = GetAnonymousLinkForUser(userId);

        if (link != null)
        {
            long ownerId = link.ReceiverId;
            var owner = GetUserByUserId(ownerId);

            if (owner != null && owner.ShowSenderUsername)
            {
                botClient.SendTextMessageAsync(
                    chatId: ownerId,
                    text: $"📩 Анонимное сообщение от @{username}:\n\n{messageText}",
                    cancellationToken: cancellationToken
                );

                botClient.SendTextMessageAsync(
                    chatId: ownerId,
                    text: $"📩 Анонимное сообщение:\n\n{messageText}",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                botClient.SendTextMessageAsync(
                    chatId: ownerId,
                    text: $"📩 Анонимное сообщение:\n\n{messageText}",
                    cancellationToken: cancellationToken
                );
            }

            SendConfirmation(botClient, chatId, cancellationToken);
            SaveMessage(userId, ownerId, username, messageText, fileId, messageType);

            Console.WriteLine($"💾 Сообщение сохранено в БД (от @{username} к {ownerId})");
        }
        else
        {
            botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Сначала откройте бота через правильную ссылку",
                cancellationToken: cancellationToken
            );
        }
    }

    private static void SendConfirmation(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var confirmKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Написать ещё ✍️", "write_more") }
        });

        botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "✅ Сообщение отправлено! Ожидайте ответ!",
            replyMarkup: confirmKeyboard,
            cancellationToken: cancellationToken
        );
    }

    private static string GenerateUniqueCode()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        string code;

        do
        {
            code = "";
            for (int i = 0; i < 6; i++)
                code += chars[random.Next(chars.Length)];
        } while (CodeExists(code));

        return code;
    }

    private static bool CodeExists(string code)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var users = db.GetCollection<BotUser>("users");
                return users.FindAll().Any(u => u.Codes.Contains(code));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return false;
        }
    }

    private static BotUser GetUserByUserId(long userId)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var users = db.GetCollection<BotUser>("users");
                return users.FindOne(u => u.UserId == userId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return null;
        }
    }

    private static BotUser GetUserByCode(string code)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var users = db.GetCollection<BotUser>("users");
                return users.FindAll().FirstOrDefault(u => u.Codes.Contains(code));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return null;
        }
    }

    private static void SaveUserWithCodes(long userId, string username, List<string> codes)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var users = db.GetCollection<BotUser>("users");
                var user = new BotUser
                {
                    UserId = userId,
                    Username = username,
                    Codes = codes,
                    CreatedAt = DateTime.Now,
                    ShowSenderUsername = false
                };
                users.Insert(user);
                Console.WriteLine($"✅ Пользователь @{username} сохранён с {codes.Count} кодами");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка сохранения пользователя: {ex.Message}");
        }
    }

    private static void UpdateUserShowSenderUsername(long userId, bool show)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var users = db.GetCollection<BotUser>("users");
                var user = users.FindById(userId);

                if (user != null)
                {
                    user.ShowSenderUsername = show;
                    users.Update(user);
                    Console.WriteLine($"✅ Режим приватности обновлён");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
        }
    }

    private static void SaveAnonymousLink(long senderId, long receiverId)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var links = db.GetCollection<AnonymousLink>("anonymous_links");
                var link = new AnonymousLink
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    CreatedAt = DateTime.Now
                };
                links.Insert(link);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
        }
    }

    private static AnonymousLink GetAnonymousLinkForUser(long userId)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var links = db.GetCollection<AnonymousLink>("anonymous_links");
                return links
                    .FindAll()
                    .Where(l => l.SenderId == userId)
                    .OrderByDescending(l => l.CreatedAt)
                    .FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return null;
        }
    }

    private static void SaveMessage(long senderId, long receiverId, string senderUsername, string messageText, string fileId, string messageType)
    {
        try
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var messages = db.GetCollection<BotMessage>("messages");
                var msg = new BotMessage
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    SenderUsername = senderUsername,
                    MessageText = messageText,
                    FileId = fileId,
                    MessageType = messageType,
                    SentAt = DateTime.Now
                };
                messages.Insert(msg);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"⚠️ Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}
