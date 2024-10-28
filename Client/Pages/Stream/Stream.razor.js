let livePlayer;

export function initializeStreamPlayer(streamUrl) {
    if (document.getElementById('streamVideo') != null) {
        var flvPlayer = mpegts.createPlayer({
            type: 'flv',
            isLive: true,
            url: streamUrl
        });
        flvPlayer.attachMediaElement(document.getElementById('streamVideo'));
        flvPlayer.load();
        console.log("test2");
    }
    console.log("test");
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