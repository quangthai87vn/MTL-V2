// SignalR client for Zalo real-time messaging
// Kết nối SignalR để nhận tin nhắn real-time từ N8N webhook

(function () {
    'use strict';

    let zaloConnection = null;
    let dotNetRef = null;

    window.zaloSignalR = {
        init: async function (dotnetRef) {
            dotNetRef = dotnetRef;

            if (zaloConnection && zaloConnection.state === signalR.HubConnectionState.Connected) {
                console.log('[ZaloSignalR] Already connected');
                return;
            }

            try {
                zaloConnection = new signalR.HubConnectionBuilder()
                    .withUrl('/hubs/zalo')
                    .withAutomaticReconnect([0, 1000, 5000, 10000, 30000])
                    .configureLogging(signalR.LogLevel.Information)
                    .build();

                // Xử lý khi nhận tin nhắn từ N8N -> SignalR -> Browser
                zaloConnection.on('ReceiveZaloMessage', function (data) {
                    console.log('[ZaloSignalR] Received message:', data);

                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnZaloMessageReceived',
                            data.userId,
                            data.message,
                            data.messageType,
                            data.mediaUrl,
                            data.userName,
                            data.userAvatar
                        );
                    }
                });

                zaloConnection.onclose(function (error) {
                    console.log('[ZaloSignalR] Connection closed', error);
                });

                zaloConnection.onreconnecting(function (error) {
                    console.log('[ZaloSignalR] Reconnecting...', error);
                });

                zaloConnection.onreconnected(function (connectionId) {
                    console.log('[ZaloSignalR] Reconnected:', connectionId);
                });

                await zaloConnection.start();
                console.log('[ZaloSignalR] Connected successfully');
            } catch (err) {
                console.error('[ZaloSignalR] Connection error:', err);
                // Thử kết nối lại sau 5 giây
                setTimeout(function () {
                    if (dotNetRef) {
                        window.zaloSignalR.init(dotNetRef);
                    }
                }, 5000);
            }
        },

        disconnect: async function () {
            if (zaloConnection) {
                try {
                    await zaloConnection.stop();
                    console.log('[ZaloSignalR] Disconnected');
                } catch (err) {
                    console.error('[ZaloSignalR] Disconnect error:', err);
                }
            }
        },

        isConnected: function () {
            return zaloConnection && zaloConnection.state === signalR.HubConnectionState.Connected;
        }
    };
})();
