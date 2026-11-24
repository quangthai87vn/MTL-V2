using MTL.APP.Models;
using MTL.APP.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace MTL.APP.Pages
{
    public partial class ZaloMessenger : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private IZaloMessengerService ZaloService { get; set; } = default!;
        [Inject] private ILogger<ZaloMessenger> Logger { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;

        private bool IsLoading { get; set; } = true;
        private bool IsLoadingMessages { get; set; } = false;
        private string SearchText { get; set; } = string.Empty;
        private ZaloConversation? SelectedConversation { get; set; }
        private List<ZaloConversation> Conversations { get; set; } = new();
        private List<ZaloMessage> Messages { get; set; } = new();
        private List<ZaloMessage> DisplayedMessages { get; set; } = new();
        private int CurrentPage { get; set; } = 1;
        private int PageSize { get; set; } = 10;
        private bool HasMoreMessages { get; set; } = false;
        private bool IsLoadingMore { get; set; } = false;
        private string NewMessage { get; set; } = string.Empty;
        private bool IsNotificationSupported { get; set; } = false;
        private bool IsNotificationEnabled { get; set; } = false;
        private int TotalUnreadCount { get; set; } = 0;
        private string? ErrorMessage { get; set; }
        private string? DebugAccessToken { get; set; }
        private N8NTokenResult? N8NTokenResult { get; set; }
        private bool IsFetchingToken { get; set; } = false;
        private string N8NTokenBorderColor => N8NTokenResult != null
            ? (N8NTokenResult.Success ? "var(--mud-palette-success)" : "var(--mud-palette-error)")
            : "var(--mud-palette-error)";

        private DotNetObjectReference<ZaloMessenger>? _dotNetRef;

        protected override async Task OnInitializedAsync()
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await InitializeNotificationsAsync();
            await InitializeSignalRAsync();
            await LoadConversationsAsync();
            IsLoading = false;

            ZaloService.OnIncomingMessage += HandleIncomingMessage;
            ZaloService.OnConversationUpdated += HandleConversationUpdated;
        }

        private async Task InitializeSignalRAsync()
        {
            try
            {
                await JS.InvokeVoidAsync("zaloSignalR.init", _dotNetRef);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to initialize SignalR connection");
            }
        }

        private async Task InitializeNotificationsAsync()
        {
            try
            {
                var supported = await JS.InvokeAsync<bool>("eval", "!!(window.Notification && Notification.permission !== 'denied')");
                IsNotificationSupported = supported;

                if (supported)
                {
                    var permission = await JS.InvokeAsync<string>("eval", "Notification.permission");
                    if (permission == "granted")
                    {
                        IsNotificationEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to check notification support");
                IsNotificationSupported = false;
            }
        }

        private async Task RequestNotificationPermission()
        {
            try
            {
                var result = await JS.InvokeAsync<string>("eval", "Notification.requestPermission()");
                IsNotificationEnabled = result == "granted";
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to request notification permission");
            }
        }

        private async Task LoadConversationsAsync()
        {
            ErrorMessage = null;
            IsLoading = true;
            StateHasChanged();

            try
            {
                var convs = await ZaloService.GetConversationsAsync();
                Conversations = convs;
                TotalUnreadCount = Conversations.Sum(c => c.UnreadCount);
                DebugAccessToken = ZaloService.GetDebugAccessToken();
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                Conversations = new List<ZaloConversation>();
                Logger.LogError(ex, "Error loading conversations: {Error}", ex.Message);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Loi khong xac dinh: {ex.Message}";
                Conversations = new List<ZaloConversation>();
                Logger.LogError(ex, "Error loading conversations");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [JSInvokable]
        public async Task OnZaloMessageReceived(string userId, string message, string messageType,
            string? mediaUrl, string? userName, string? userAvatar)
        {
            try
            {
                ZaloService.HandleIncomingMessage(userId, message, messageType, mediaUrl, userName, userAvatar);

                await InvokeAsync(() =>
                {
                    StateHasChanged();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling incoming Zalo message");
            }
        }

        private void HandleIncomingMessage(object? sender, ZaloMessage msg)
        {
            InvokeAsync(() =>
            {
                var existing = Messages.FirstOrDefault(m => m.Id == msg.Id);
                if (existing == null)
                {
                    Messages.Add(msg);
                    if (SelectedConversation != null && DisplayedMessages.Count < PageSize)
                    {
                        DisplayedMessages.Add(msg);
                    }
                    else if (SelectedConversation != null)
                    {
                        HasMoreMessages = true;
                    }
                    StateHasChanged();
                }

                if (SelectedConversation != null)
                {
                    try
                    {
                        var isFocused = JS.InvokeAsync<bool>("eval", "document.hasFocus()").Result;
                        if (!isFocused && IsNotificationEnabled)
                        {
                            _ = ShowBrowserNotification($"Tin nhan moi tu {SelectedConversation.Name}",
                                msg.Content.Length > 100 ? msg.Content[..100] + "..." : msg.Content);
                        }
                    }
                    catch { }
                }
            });
        }

        private void HandleConversationUpdated(object? sender, ZaloConversation conv)
        {
            InvokeAsync(() =>
            {
                var existing = Conversations.FirstOrDefault(c => c.Id == conv.Id);
                if (existing != null)
                {
                    existing.LastMessage = conv.LastMessage;
                    existing.Time = conv.Time;
                    existing.UnreadCount = conv.UnreadCount;
                }
                else
                {
                    Conversations.Insert(0, conv);
                }

                TotalUnreadCount = Conversations.Sum(c => c.UnreadCount);
                StateHasChanged();
            });
        }

        private IEnumerable<ZaloConversation> FilteredConversations
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return Conversations.OrderByDescending(c => c.LastMessageTime);

                return Conversations
                    .Where(c => c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                (c.LastMessage?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                    .OrderByDescending(c => c.LastMessageTime);
            }
        }

        private async Task SelectConversation(ZaloConversation conv)
        {
            SelectedConversation = conv;
            Messages = new List<ZaloMessage>();
            DisplayedMessages = new List<ZaloMessage>();
            CurrentPage = 1;
            ErrorMessage = null;

            await ZaloService.MarkAsReadAsync(conv.Id);
            conv.UnreadCount = 0;
            TotalUnreadCount = Conversations.Sum(c => c.UnreadCount);

            IsLoadingMessages = true;
            StateHasChanged();

            try
            {
                var allMessages = await ZaloService.GetMessagesAsync(conv.Id, 1, 100);
                Messages = allMessages;
                HasMoreMessages = Messages.Count > PageSize;
                DisplayedMessages = Messages.Take(PageSize).ToList();
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = $"Loi khi load tin nhan: {ex.Message}";
                Logger.LogError(ex, "Error loading messages for {UserId}", conv.Id);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Loi khi load tin nhan: {ex.Message}";
                Logger.LogError(ex, "Error loading messages for {UserId}", conv.Id);
            }
            finally
            {
                IsLoadingMessages = false;
            }

            StateHasChanged();

            await Task.Delay(100);
            await JS.InvokeVoidAsync("scrollToBottom", "zalo-messages-container");
        }

        private async Task LoadMoreMessages()
        {
            if (IsLoadingMore || !HasMoreMessages || SelectedConversation == null)
                return;

            IsLoadingMore = true;
            StateHasChanged();

            await Task.Delay(300);

            CurrentPage++;
            var takeCount = PageSize * CurrentPage;
            DisplayedMessages = Messages.Take(takeCount).ToList();
            HasMoreMessages = Messages.Count > takeCount;

            IsLoadingMore = false;
            StateHasChanged();
        }

        private async Task RefreshMessages()
        {
            IsLoading = true;
            ErrorMessage = null;
            await Task.Delay(300);

            try
            {
                await LoadConversationsAsync();
            }
            catch { }

            if (SelectedConversation != null)
            {
                try
                {
                    var allMessages = await ZaloService.GetMessagesAsync(SelectedConversation.Id, 1, 100);
                    Messages = allMessages;
                    HasMoreMessages = Messages.Count > PageSize;
                    DisplayedMessages = Messages.Take(PageSize).ToList();
                }
                catch (InvalidOperationException ex)
                {
                    ErrorMessage = $"Loi khi load tin nhan: {ex.Message}";
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Loi khi load tin nhan: {ex.Message}";
                }
            }

            IsLoading = false;
            StateHasChanged();
        }

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(NewMessage) || SelectedConversation == null)
                return;

            var msg = new ZaloMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = NewMessage,
                Type = "text",
                IsFromUser = false,
                Status = "sending",
                Time = DateTime.Now.ToString("HH:mm"),
                CreatedAt = DateTime.Now
            };

            Messages.Add(msg);
            if (DisplayedMessages.Count < PageSize)
            {
                DisplayedMessages.Add(msg);
            }
            else
            {
                HasMoreMessages = true;
            }
            SelectedConversation.LastMessage = NewMessage;
            SelectedConversation.Time = msg.Time;

            NewMessage = string.Empty;
            StateHasChanged();

            try
            {
                var result = await ZaloService.SendMessageAsync(SelectedConversation.Id, msg.Content);
                msg.Id = result.Id;
                msg.Status = result.Status;
            }
            catch (InvalidOperationException ex)
            {
                msg.Status = "failed";
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                msg.Status = "failed";
                ErrorMessage = $"Loi gui tin nhan: {ex.Message}";
            }

            StateHasChanged();
            await JS.InvokeVoidAsync("scrollToBottom", "zalo-messages-container");
        }

        private async Task HandleKeyUp(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !e.ShiftKey)
            {
                await SendMessage();
            }
        }

        private async Task AttachFile()
        {
            await JS.InvokeVoidAsync("triggerFileClick", "zalo-file-input");
        }

        private async Task HandleFileSelected(IBrowserFile file)
        {
            await JS.InvokeVoidAsync("alert", "Tinh nang dinh kem file se duoc them sau");
        }

        private async Task ShowBrowserNotification(string title, string body)
        {
            try
            {
                await JS.InvokeVoidAsync("eval", $@"
                    if (Notification.permission === 'granted') {{
                        new Notification('{title}', {{
                            body: '{body}',
                            icon: '/images/zalo-icon.png',
                            tag: 'zalo-message'
                        }});
                    }}
                ");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to show browser notification");
            }
        }

        private async Task FetchN8NToken()
        {
            IsFetchingToken = true;
            StateHasChanged();

            try
            {
                var result = await ZaloService.ForceFetchTokenFromN8NAsync();
                N8NTokenResult = result;
                DebugAccessToken = result.AccessToken;
            }
            catch (Exception ex)
            {
                N8NTokenResult = new N8NTokenResult(false, null, null, null, ex.Message, null, false);
            }
            finally
            {
                IsFetchingToken = false;
                StateHasChanged();
            }
        }

        private async Task CopyToClipboard(string text)
        {
            try
            {
                await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
                Snackbar.Add("Da copy vao clipboard!", Severity.Success);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to copy to clipboard");
            }
        }

        public async ValueTask DisposeAsync()
        {
            ZaloService.OnIncomingMessage -= HandleIncomingMessage;
            ZaloService.OnConversationUpdated -= HandleConversationUpdated;
            _dotNetRef?.Dispose();

            try
            {
                await JS.InvokeVoidAsync("zaloSignalR.disconnect");
            }
            catch { }
        }
    }
}
