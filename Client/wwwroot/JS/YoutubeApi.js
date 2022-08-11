function runYoutubeApi() {
//YouTube embed with YouTube Iframe API
    var tag = document.createElement('script');

    tag.src = "https://www.youtube.com/iframe_api";
    var firstScriptTag = document.getElementsByTagName('script')[0];
    firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
}
var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}
// YouTube embed player details
var player;
function onYouTubeIframeAPIReady() {
    if (typeof YT !== 'undefined') {
        player = new YT.Player('player', {
            videoId: 'xhZQ46ZRzq0',
            playerVars: {
                'playsinline': 1
            },
            events: {
                'onStateChange': stateChange
            }
        });
    }
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

function stateChange(event) {
    dotNetHelper.invokeMethodAsync('StateChange', event.data);
}