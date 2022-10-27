let initialVideoId;
function runYoutubeApi(videoId) {
//YouTube embed with YouTube Iframe API
    initialVideoId = videoId;
    setTimeout(() => {
        var tag = document.createElement('script');

        tag.src = "https://www.youtube.com/iframe_api";
        var firstScriptTag = document.getElementsByTagName('script')[0];
        firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
    }, 1000);
}

var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

// YouTube embed player details
var player;
function onYouTubeIframeAPIReady() {
    player = new YT.Player('player', {
        height: '390',
        width: '640',
        videoId: initialVideoId,
        playerVars: {
            'playsinline': 1,
            'autoplay': 1
        },
        events: {
            'onStateChange': youtubeStateChange
        }
    });
}

setInterval(function () {
    if (typeof YT !== 'undefined' && player !== 'undefined') {
        var currentTime = getCurrentTime();
        if (currentTime && player.getPlayerState() !== 0) {
            dotNetHelper.invokeMethodAsync('ProgressChange', currentTime);
        }
    }
}, 300)

//functions
function playYT() {
    player.playVideo();
}

function pauseYT() {
    player.pauseVideo();
}

function getCurrentTime() {
    return player.getCurrentTime();
}

function setCurrentYoutubeVideoTime(time) {
    let success = true;
    try {
        player.seekTo(time, true);
    } catch (e) {
        success = false;
    }
    return success;
}

function loadVideo(videoId) {
    player.loadVideoById(videoId);
}

function youtubeStateChange(event) {
    dotNetHelper.invokeMethodAsync('StateChange', event.data);
}

function youtubeDestroy() {
    if (player) {
        player.destroy();
    }
}