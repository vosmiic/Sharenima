var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

function initializeStreamPlayer(streamUrl) {
    if (document.getElementById('streamPlayer') != null) {
        const livePlayer = OvenPlayer.create('streamPlayer', {
            sources: [
                {
                    label: 'label_for_webrtc',
                    // Set the type to 'webrtc'
                    type: 'webrtc',
                    // Set the file to WebRTC Signaling URL with OvenMediaEngine 
                    file: streamUrl
                }
            ]
        });
    }
}
