let initialVideoId;
let comparisonVideoId;
let autoplay;
let youtubeLastExecution = new Date();


function runYoutubeApi(videoId, playingState, dbVideoId) {
    if (youtubeCheckIfPlayerExists()) return;
    initialVideoId = videoId;
    comparisonVideoId = dbVideoId;
    autoplay = playingState;

    onYouTubeIframeAPIReady();
    setTimeout(() => {
        let existingTag = document.querySelector(".plyr");
        if (existingTag) {
            var playerConnected = false;
            try {
                playerConnected = player.embed.getIframe().isConnected;
            } catch (_) {

            }
            if (playerConnected) {
                changeYTVideoSource(videoId);
            } else {
                onYouTubeIframeAPIReady();
            }
        } else {
            onYouTubeIframeAPIReady();
        }
    }, 1000);
}

var dotNetHelper;

function setDotNetHelper(value) {
    dotNetHelper = value;
}

let player;

function onYouTubeIframeAPIReady() {
    if (youtubeCheckIfPlayerExists()) return;
    player = new Plyr(document.getElementById('player'), {
        playsinline: true
    });

    let ignoreFirst = true;
    player.on('ready', (event) => {
        if (ignoreFirst) {
            // ignore the first ready call
            ignoreFirst = false;
            return;
        }
        youtubeOnReady(event);
    });

    player.on('statechange', (event) => {
        youtubeStateChange(event.detail.code);
    });

    player.on('error', (event) => {
        youtubeOnError(event.error);
    });
    
    player.on('volumechange', (event) => {
        setPlayerVolumeCookie(event.detail.plyr.volume * 100);
    });

    let lastCheck = 0;
    player.on('timeupdate', (event) => {
        const difference = lastCheck - event.detail.plyr.currentTime;
        if (Math.abs(difference) >= 1) {
            dotNetHelper.invokeMethodAsync('ProgressChange', comparisonVideoId, event.detail.plyr.currentTime, false);
            lastCheck = event.detail.plyr.currentTime;
        }
    })
    
    player.on('seeked', (event) => {
        if (new Date() - youtubeLastExecution >= 100) {
            updateBackendProgress(event.detail.plyr.currentTime, comparisonVideoId, youtubeLastExecution);
        }
    })
}

function youtubeOnReady(event) {
    var playerVolume = getPlayerVolumeCookie();
    if (playerVolume != null) {
        player.volume = playerVolume / 100;
    }
    dotNetHelper.invokeMethodAsync('RequestInitialVideoTime').then((time) => {
        player.embed.seekTo(time);
        console.log(time);
    });

    if (autoplay) {
        playYT();
    }
    console.log("setting ready true")
    dotNetHelper.invokeMethodAsync('SetReady', true);
}

/*let lastUpdateTime = null;
let fastLastUpdateTime = null;
setInterval(function () {
    if (player !== 'undefined' && comparisonVideoId) {
        var currentTime = getCurrentTime();
        if (currentTime && !player.paused && !player.ended) {
            if (!initialLoad) {
                dotNetHelper.invokeMethodAsync('ProgressChange', comparisonVideoId, currentTime, false);
                lastUpdateTime = currentTime;
                fastLastUpdateTime = currentTime;
            }
            initialLoad = false;
        }
    }
}, 1000)*/

//functions
function playYT() {
    player.play();
    setTimeout(() => {
        if (player.paused) {
            // user must have not interacted with the site yet, got to mute and then play again
            player.muted = true;
            player.play();
        }
    }, 500);
}

function pauseYT() {
    player.pause();
}

function getCurrentTime() {
    return player.currentTime;
}

function setCurrentYoutubeVideoTime(time, currentVideoId) {
    if (currentVideoId === comparisonVideoId) {
        let success = true;
        try {
            player.currentTime = time;
        } catch (e) {
            success = false;
        }
        return success;
    }

    return false;
}

function changeYTVideoSource(videoId) {
    try {
        player.source = {
            "type": "video",
            "sources": [
                {
                    "src": videoId,
                    "provider": "youtube"
                }
            ]
        };
        dotNetHelper.invokeMethodAsync('SetReady', true);
    } catch {
    }
}

function youtubeStateChange(event) {
    dotNetHelper.invokeMethodAsync('StateChange', event, comparisonVideoId);
}

function youtubeDestroy() {
    player.destroy();
}

function youtubeOnError(error) {
    dotNetHelper.invokeMethodAsync('OnPlayerError', error, comparisonVideoId);
}

function youtubeCheckIfPlayerExists() {
    try {
        var test = player.source;
        return true;
    } catch (e) {
        console.log(e);
        return false;
    }
}