function runYoutubeApi() {
//YouTube embed with YouTube Iframe API
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
    dotNetHelper.invokeMethodAsync('RequestVideo').then((videoId) => {
        player = new YT.Player('player', {
            height: '390',
            width: '640',
            videoId: videoId,
            playerVars: {
                'playsinline': 1,
                'autoplay': 1
            },
            events: {
                'onStateChange': stateChange
            }
        });
    });
}

setInterval(function () {
    if (typeof YT !== 'undefined') {
        var currentTime = getCurrentTime();
        console.log(currentTime);
        if (currentTime) {
            dotNetHelper.invokeMethodAsync('ProgressChange', currentTime);
        }
    }
}, 500)

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

function setCurrentTime(time) {
    player.seekTo(time, true);
}

function loadVideo(videoId) {
    player.loadVideoById(videoId);
}

function stateChange(event) {
    dotNetHelper.invokeMethodAsync('StateChange', event.data);
}