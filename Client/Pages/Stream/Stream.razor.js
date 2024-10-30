let livePlayer;

export function initializeStreamPlayer(streamUrl) {
    if (document.getElementById('streamVideo') != null) {
        livePlayer = mpegts.createPlayer({
            type: 'flv',
            isLive: true,
            url: streamUrl
        });
        livePlayer.attachMediaElement(document.getElementById('streamVideo'));
        livePlayer.load();
    }
}

export function loadNewStream(streamUrl) {
    if (livePlayer) {
        livePlayer.destroy();
        initializeStreamPlayer(streamUrl);
    }
}