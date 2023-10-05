let initialVideoId;
let comparisonVideoId;
let autoplay;

function runYoutubeApi(videoId, playingState, dbVideoId) {
//YouTube embed with YouTube Iframe API
    initialVideoId = videoId;
    comparisonVideoId = dbVideoId;
    autoplay = playingState;
    setTimeout(() => {
        let existingTag = document.querySelector("script[src='https://www.youtube.com/iframe_api']");
        if (existingTag) {
            var playerConnected = false;
            try {
                playerConnected = player.getIframe().isConnected;
            } catch (_) {
                
            }
            if (playerConnected) {
                changeYTVideoSource(videoId);
            } else {
                onYouTubeIframeAPIReady();
            }
        } else {
            var tag = document.createElement('script');

            tag.src = "https://www.youtube.com/iframe_api";
            var firstScriptTag = document.getElementsByTagName('script')[0];
            firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
        }
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
            'autohide': 1,
            'controls': 1,
            'iv_load_policy': 3,
            'rel': 0
        },
        events: {
            'onReady': youtubeOnReady,
            'onStateChange': youtubeStateChange,
            'onError': youtubeOnError
        }
    });
    
    window.addEventListener("message", function (event) {
        if (event.source === player.getIframe().contentWindow) {
            var data = JSON.parse(event.data);
            if (data.event === "infoDelivery" && data.info && data.info.volume) {
               setPlayerVolumeCookie(data.info.volume);
            }
        }

    })
}

function youtubeOnReady(event) {
    var playerVolume = getPlayerVolumeCookie();
    if (playerVolume != null) {
        player.setVolume(playerVolume);
    }
    dotNetHelper.invokeMethodAsync('RequestInitialVideoTime').then((time) => {
        event.target.seekTo(time);
        pauseYT();
    });

    if (autoplay) {
        playYT();
    }
    console.log("setting ready true")
    dotNetHelper.invokeMethodAsync('SetReady', true);
}

let initialLoad = true;
let lastUpdateTime = null;
let fastLastUpdateTime = null;
setInterval(function () {
    if (typeof YT !== 'undefined' && player !== 'undefined' && comparisonVideoId) {
        
        
        
        var currentTime = getCurrentTime();
        if (currentTime && player.getPlayerState() !== 0 && player.getPlayerState() !== 2) {
            if (!initialLoad) {
                let playerPlaybackRate = player.getPlaybackRate();
                dotNetHelper.invokeMethodAsync('ProgressChange', comparisonVideoId, currentTime, lastUpdateTime != null && Math.abs(currentTime - lastUpdateTime - 1) > 0.2 && playerPlaybackRate === 1);
                lastUpdateTime = currentTime;
                fastLastUpdateTime = currentTime;
            }
            initialLoad = false;
        }
    }
}, 1000)

let lastStoredSystemTime = null;
const updateInterval = 50;

// below only handles seeked
setInterval(function () {
    if (typeof YT !== 'undefined' && player !== 'undefined' && comparisonVideoId) {
        if (lastStoredSystemTime == null) {
            lastStoredSystemTime = new Date();
        } else {
            lastStoredSystemTime = new Date();
            if (Math.abs((new Date().getTime() - lastStoredSystemTime.getTime()) - updateInterval) > (updateInterval / 2)) {
                console.log("Lag detected! Presuming seek detection is false positive...")
                return;
            }
        }
        
        var currentTime = getCurrentTime();
        let playerPlaybackRate = player.getPlaybackRate();
        if (fastLastUpdateTime != null && Math.abs(currentTime - fastLastUpdateTime - 0.05) > 1.1 && playerPlaybackRate === 1) {
            console.log(Math.abs(currentTime - fastLastUpdateTime - 0.05));
            if (!initialLoad) {
                dotNetHelper.invokeMethodAsync('ProgressChange', comparisonVideoId, currentTime, true);
                lastUpdateTime = currentTime;
                fastLastUpdateTime = currentTime;
            }
            initialLoad = false;
        }
    }
}, updateInterval);

//functions
function playYT() {
    player.playVideo();
    setTimeout(() => {
        if (player.getPlayerState() === -1) {
            // user must have not interacted with the site yet, got to mute and then play again
            player.mute();
            player.playVideo();
        }
    }, 500);
}

function pauseYT() {
    player.pauseVideo();
}

function getCurrentTime() {
    return player.getCurrentTime();
}

function setCurrentYoutubeVideoTime(time, currentVideoId) {
    if (currentVideoId === comparisonVideoId) {
        let success = true;
        try {
            player.seekTo(time, true);
        } catch (e) {
            success = false;
        }
        return success;
    }

    return false;
}

function changeYTVideoSource(videoId) {
    try {
        player.loadVideoById(videoId);
        dotNetHelper.invokeMethodAsync('SetReady', true);
    } catch {
    }
}

function youtubeStateChange(event) {
    dotNetHelper.invokeMethodAsync('StateChange', event.data, comparisonVideoId);
}

function youtubeDestroy() {
    document.getElementById("youtubeContainer").textContent = '';
}

function youtubeOnError(error) {
    dotNetHelper.invokeMethodAsync('OnPlayerError', error.data, comparisonVideoId);
}

function youtubeCheckIfPlayerExists() {
    try {
        player.getIframe();
        return true;
    } catch (_) {
        return false;
    }
}