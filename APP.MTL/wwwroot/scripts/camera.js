// Camera snapshot download — converts base64 to Blob and triggers browser download
function downloadBase64Image(base64Data, fileName) {
    try {
        // Convert base64 to Uint8Array
        var binaryString = atob(base64Data);
        var bytes = new Uint8Array(binaryString.length);
        for (var i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }

        var blob = new Blob([bytes], { type: 'image/jpeg' });
        var url = URL.createObjectURL(blob);

        var link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        // Free memory
        setTimeout(function() { URL.revokeObjectURL(url); }, 1000);
    } catch (e) {
        console.error('downloadBase64Image error:', e);
    }
}

window.downloadBase64Image = downloadBase64Image;
