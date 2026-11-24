// JavaScript helper functions for Facebook Chat UI

// Scroll element to bottom
window.scrollToBottom = function (elementId) {
    var element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// Open image in new tab
window.openImage = function (url) {
    if (url) {
        window.open(url, '_blank');
    }
};

// Play notification sound
window.playNotificationSound = function () {
    try {
        var audio = new Audio('/sounds/notification.mp3');
        audio.play().catch(function () { });
    } catch (e) { }
};

// Format timestamp for display
window.formatTimestamp = function (timestamp) {
    var date = new Date(timestamp);
    var now = new Date();
    var diffMs = now - date;
    var diffMins = Math.floor(diffMs / 60000);
    var diffHours = Math.floor(diffMs / 3600000);
    var diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Vừa xong';
    if (diffMins < 60) return diffMins + ' phút';
    if (diffHours < 24) return diffHours + ' giờ';
    if (diffDays < 7) return diffDays + ' ngày';
    
    return date.toLocaleDateString('vi-VN');
};

// Trigger haptics on mobile
window.triggerHaptic = function () {
    if (navigator.vibrate) {
        navigator.vibrate(10);
    }
};

// Copy text to clipboard
window.copyToClipboard = function (text) {
    if (navigator.clipboard) {
        navigator.clipboard.writeText(text);
    }
};

// Trigger hidden file input
window.triggerFileClick = function (inputId) {
    var input = document.getElementById(inputId);
    if (input) {
        input.click();
    }
};

// Format file size
window.formatFileSize = function (bytes) {
    if (bytes === 0 || bytes === null) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

// Download file
window.downloadFile = function (url, filename) {
    if (url) {
        var a = document.createElement('a');
        a.href = url;
        a.download = filename || 'download';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }
};

// Open image in modal
window.openImageModal = function (imageUrl) {
    if (imageUrl) {
        var win = window.open(imageUrl, '_blank', 'width=800,height=600');
        win?.focus();
    }
};

// Trigger hidden file input
window.triggerFileClick = function (inputId) {
    var input = document.getElementById(inputId);
    if (input) {
        input.click();
    }
};

// Format file size
window.formatFileSize = function (bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

// Download file
window.downloadFile = function (url, filename) {
    if (url) {
        var a = document.createElement('a');
        a.href = url;
        a.download = filename || 'download';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }
};

// Open image in modal
window.openImageModal = function (imageUrl) {
    if (imageUrl) {
        var win = window.open(imageUrl, '_blank', 'width=800,height=600');
        win?.focus();
    }
};
