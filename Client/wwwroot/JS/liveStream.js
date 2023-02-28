var dotNetHelper;
let livePlayer;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

function initializeStreamPlayer(streamUrl) {
    if (document.getElementById('streamPlayer') != null) {
        livePlayer = OvenPlayer.create('streamPlayer', {
            sources: [
                {
                    label: 'LIVE',
                    type: 'webrtc',
                    file: streamUrl
                }
            ]
        });
    }
}

function loadNewStream(streamUrl) {
    if (livePlayer) {
        livePlayer.load({
            sources: [
                {
                    label: 'LIVE',
                    type: 'webrtc',
                    file: streamUrl
                }
            ]
        })
    }
}