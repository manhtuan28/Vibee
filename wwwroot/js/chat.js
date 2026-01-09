"use strict";

const rtcConfig = {
    iceServers: [
        { urls: 'stun:stun.l.google.com:19302' },
        { urls: 'stun:stun1.l.google.com:19302' }
    ]
};

let localStream;
let peerConnection;
let isCaller = false;
let callStartTime = null;
let callTimerInterval = null;
let replyingToId = null;

var connection = window.connection;

document.addEventListener("DOMContentLoaded", function () {
    if (document.getElementById('chat-sidebar-list')) {
        reloadSidebar();
    }

    const urlParams = new URLSearchParams(window.location.search);
    const autoAccept = urlParams.get('autoAccept');
    const targetId = urlParams.get('userId');

    const callerName = urlParams.get('name');
    const callerAvatar = urlParams.get('avatar');

    if (autoAccept === 'true' && targetId) {
        //console.log("🔄 Đang tự động kết nối cuộc gọi với:", targetId);

        window.incomingCallerId = targetId;
        if (callerName) window.incomingCallerName = decodeURIComponent(callerName);
        if (callerAvatar) window.incomingCallerAvatar = decodeURIComponent(callerAvatar);

        if (typeof updateVideoInfo === 'function') {
            updateVideoInfo(window.incomingCallerName, window.incomingCallerAvatar);
        }

        const checkConn = setInterval(() => {
            if (connection && connection.state === "Connected") {
                clearInterval(checkConn);
                acceptIncomingCall();

                window.history.replaceState({}, document.title, "/Chat?userId=" + targetId);
            }
        }, 500);
    }
});


if (connection) {
    connection.on("ReceiveMessage", function (senderId, content, createdAt, msgId, type, replyData) {
        if (typeof window.currentReceiverId !== 'undefined' && window.currentReceiverId === senderId) {
            if (typeof appendMainMessage === 'function') {
                appendMainMessage({
                    id: msgId, content: content, createdAt: createdAt,
                    isMine: false, type: type, replyTo: replyData
                });
                scrollMainToBottom();
                connection.invoke("MarkAsRead", senderId);
            }
        }

        var chatBox = document.getElementById(`chat-box-${senderId}`);
        if (chatBox) {
            var body = chatBox.querySelector('.chat-body');
            appendMessage(body, content, 'theirs', type);
            scrollToBottom(body);
            connection.invoke("MarkAsRead", senderId);
        }

        if (window.currentReceiverId !== senderId && !chatBox) {
            if (typeof playMsgSound === 'function') playMsgSound();
            if (typeof updateMsgBadge === 'function') updateMsgBadge();
        }
        reloadSidebar();
    });

    connection.on("MessageSent", function (receiverId, content, createdAt, msgId, type, replyData) {
        if (typeof window.currentReceiverId !== 'undefined' && window.currentReceiverId === receiverId) {
            appendMainMessage({
                id: msgId, content: content, createdAt: createdAt,
                isMine: true, type: type, replyTo: replyData
            });
            scrollMainToBottom();
            cancelReply();
        }

        var chatBox = document.getElementById(`chat-box-${receiverId}`);
        if (chatBox) {
            var body = chatBox.querySelector('.chat-body');
            appendMessage(body, content, 'mine', type);
            scrollToBottom(body);
            var input = chatBox.querySelector('.chat-input');
            if (input) { input.value = ''; input.focus(); }
        }
        reloadSidebar();
    });

    connection.on("MessagesRead", function (readerId) {
        if (window.currentReceiverId === readerId) {
            document.querySelectorAll('.seen-avatar').forEach(el => el.remove());
            const myMsgs = document.querySelectorAll('.msg-mine .msg-content-wrapper');
            if (myMsgs.length > 0) {
                const lastMsg = myMsgs[myMsgs.length - 1];
                const seenIcon = document.createElement('img');
                const headerAvatar = document.getElementById('header-avatar');
                if (headerAvatar) {
                    seenIcon.src = headerAvatar.src;
                    seenIcon.className = 'seen-avatar';
                    lastMsg.appendChild(seenIcon);
                }
            }
        }
    });

    connection.on("MessageDeleted", function (msgId, mode) {
        const msgEl = document.getElementById(`msg-${msgId}`);
        if (msgEl) {
            if (mode === "everyone") {
                const bubble = msgEl.querySelector('.msg-bubble');
                if (bubble) {
                    bubble.innerText = "Tin nhắn đã bị thu hồi";
                    bubble.classList.add("msg-deleted");
                    const media = msgEl.querySelector('img.chat-image, video');
                    if (media) media.remove();
                    const reply = msgEl.querySelector('.reply-quote');
                    if (reply) reply.remove();
                }
            } else if (mode === "me") {
                msgEl.remove();
            }
        }
        reloadSidebar();
    });

    connection.on("CallAccepted", async function (receiverId) {
        updateCallStatus("Đang thiết lập...");
        createPeerConnection(receiverId);
        try {
            const offer = await peerConnection.createOffer();
            await peerConnection.setLocalDescription(offer);
            connection.invoke("SendSignal", receiverId, JSON.stringify({ type: "offer", sdp: offer }));
        } catch (e) { console.error(e); }
    });

    connection.on("CallRejected", function () {
        //console.log("Cuộc gọi bị từ chối hoặc người dùng bận.");
        endCall(false);
    });

    connection.on("CallEnded", function () {
        //console.log("Đối phương đã kết thúc cuộc gọi.");
        endCall(false);
    });

    connection.on("ReceiveSignal", async function (senderId, data) {
        const signal = JSON.parse(data);
        let targetId = isCaller ? (window.currentChattingUserId || window.currentReceiverId) : window.incomingCallerId;
        if (!targetId) targetId = senderId;

        if (!peerConnection) createPeerConnection(targetId);

        try {
            if (signal.type === "offer") {
                await peerConnection.setRemoteDescription(new RTCSessionDescription(signal.sdp));
                const answer = await peerConnection.createAnswer();
                await peerConnection.setLocalDescription(answer);
                connection.invoke("SendSignal", senderId, JSON.stringify({ type: "answer", sdp: answer }));
            } else if (signal.type === "answer") {
                await peerConnection.setRemoteDescription(new RTCSessionDescription(signal.sdp));
            } else if (signal.type === "candidate" && signal.candidate) {
                await peerConnection.addIceCandidate(new RTCIceCandidate(signal.candidate));
            }
        } catch (e) { console.error(e); }
    });
}

function reloadSidebar() {
    const list = document.getElementById('chat-sidebar-list');
    if (!list) return;

    fetch('/Chat/GetSidebarUsers')
        .then(res => res.json())
        .then(res => {
            if (!res.success) return;
            list.innerHTML = '';
            res.data.forEach(u => {
                const isActive = (window.currentReceiverId === u.id) ? 'active' : '';
                const isUnread = !u.isRead ? 'unread' : '';
                const time = (typeof moment !== 'undefined') ? moment.utc(u.lastTime).local().fromNow() : '';
                const contentStyle = !u.isRead ? 'font-weight:700;color:#000;' : 'color:#65676b;';
                const onlineHtml = u.isOnline ? `<div class="position-absolute border border-2 border-white rounded-circle bg-success" style="width:14px; height:14px; bottom:0; right:0;"></div>` : '';

                list.innerHTML += `
                    <div class="chat-item ${isActive} ${isUnread}" id="user-item-${u.id}" onclick="loadFullChat('${u.id}', '${u.fullName}', '${u.avatarUrl}')">
                        <div class="position-relative me-3">
                            <img src="${u.avatarUrl}" class="rounded-circle border" width="56" height="56" style="object-fit:cover">
                            ${onlineHtml}
                        </div>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="user-name text-truncate" style="font-size:1rem;">${u.fullName}</div>
                            <div class="d-flex justify-content-between align-items-center mt-1">
                                <div class="last-msg text-truncate small" style="max-width:160px; ${contentStyle}">
                                    ${u.lastMessage || 'Bắt đầu trò chuyện'}
                                </div>
                                <div class="small text-muted ms-2" style="font-size:10px">${u.lastMessage ? time : ''}</div>
                            </div>
                        </div>
                        ${!u.isRead && u.unreadCount > 0 ? `<div class="unread-dot"></div>` : ''}
                    </div>`;
            });
        });
}

function loadFullChat(userId, fullName, avatar) {
    window.currentReceiverId = userId;

    const welcome = document.getElementById('chat-welcome');
    const interfaceDiv = document.getElementById('chat-interface');
    if (welcome) welcome.classList.add('d-none');
    if (interfaceDiv) {
        interfaceDiv.classList.remove('d-none');
        interfaceDiv.classList.add('d-flex');
    }

    document.querySelectorAll('.chat-item').forEach(el => el.classList.remove('active'));
    const activeItem = document.getElementById(`user-item-${userId}`);
    if (activeItem) {
        activeItem.classList.add('active');
        activeItem.classList.remove('unread');
        const dot = activeItem.querySelector('.unread-dot');
        if (dot) dot.remove();
    }

    document.getElementById('header-name').innerText = fullName;
    const headerAvatar = document.getElementById('header-avatar');
    if (headerAvatar) headerAvatar.src = avatar;

    const body = document.getElementById('main-chat-body');
    body.innerHTML = '<div class="h-100 d-flex align-items-center justify-content-center"><div class="spinner-border text-primary"></div></div>';

    fetch(`/Message/GetConversation/${userId}`)
        .then(res => res.json())
        .then(res => {
            if (res.success) {
                body.innerHTML = '<div style="height: 20px;"></div>';
                res.data.forEach(msg => {
                    appendMainMessage({
                        id: msg.id, content: msg.content, createdAt: msg.createdAt,
                        isMine: msg.isMine, type: msg.type || 'Text', replyTo: msg.replyTo
                    });
                });
                scrollMainToBottom();
                connection.invoke("MarkAsRead", userId);
                reloadSidebar();
            }
        });
}

function appendMainMessage(msg) {
    const body = document.getElementById('main-chat-body');
    if (!body) return;

    const isMine = msg.isMine;
    const alignClass = isMine ? 'msg-mine' : 'msg-theirs';
    let contentHtml = '';
    let isCallMsg = msg.type === 'Call';

    if (isCallMsg) {
        const isMissed = msg.content.includes("nhỡ") || msg.content.includes("bận");
        const icon = isMissed ? 'bi-telephone-x-fill text-danger' : 'bi-camera-video-fill text-primary';
        contentHtml = `
            <div class="msg-call d-flex align-items-center gap-2 p-2 border rounded bg-white" style="min-width: 200px;">
                <div class="call-icon-box rounded-circle d-flex align-items-center justify-content-center" style="width:32px;height:32px;background:#f0f2f5;">
                    <i class="bi ${icon}"></i>
                </div>
                <div>
                    <div class="fw-bold small text-dark">${msg.content}</div>
                    <div class="small text-muted" style="font-size:0.7rem">${isMissed ? 'Nhấn để gọi lại' : 'Đã kết thúc'}</div>
                </div>
            </div>`;
    } else {
        const cleanContent = msg.content ? msg.content.replace(/\\/g, "/") : "";
        if (msg.type === 'Image') contentHtml = `<img src="${cleanContent}" class="chat-image" onclick="window.open(this.src)">`;
        else if (msg.type === 'Video') contentHtml = `<video src="${cleanContent}" class="chat-video" controls preload="metadata"></video>`;
        else contentHtml = msg.content;
    }

    let replyHtml = '';
    if (msg.replyTo && !isCallMsg) {
        let replyName = msg.replyTo.senderName || "Người dùng";
        let replyContent = msg.replyTo.content || "...";
        if (typeof myId !== 'undefined' && msg.replyTo.senderId === myId) replyName = "bạn";

        const headerNameEl = document.getElementById('header-name');
        const actionPrefix = isMine ? 'Bạn đã trả lời' : `${headerNameEl ? headerNameEl.innerText : 'Ai đó'} đã trả lời`;

        replyHtml = `
            <div class="reply-quote mb-1">
                <div class="small fw-bold"><i class="bi bi-reply-fill me-1"></i>${actionPrefix} <strong>${replyName}</strong></div>
                <div class="text-truncate small text-muted" style="font-style:italic;">"${replyContent}"</div>
            </div>`;
    }

    const div = document.createElement('div');
    div.id = `msg-${msg.id}`;
    div.className = `msg-row ${alignClass}`;
    function getActionsHtml(msgId) {
        return `
            <div class="msg-actions">
                <div class="action-btn" onclick="startReply('${msgId}')" title="Trả lời"><i class="bi bi-reply-fill"></i></div>
                <div class="dropdown">
                    <div class="action-btn" data-bs-toggle="dropdown"><i class="bi bi-three-dots-vertical"></i></div>
                    <ul class="dropdown-menu border-0 shadow" style="border-radius:12px;">
                        <li><a class="dropdown-item text-danger small py-2" href="javascript:void(0)" onclick="deleteMsg(${msgId}, 'me')"><i class="bi bi-trash me-2"></i>Xóa ở phía tôi</a></li>
                        <li><a class="dropdown-item text-danger small py-2" href="javascript:void(0)" onclick="deleteMsg(${msgId}, 'everyone')"><i class="bi bi-x-circle me-2"></i>Thu hồi</a></li>
                    </ul>
                </div>
            </div>`;
    }

    const timeTooltip = (typeof moment !== 'undefined') ? moment.utc(msg.createdAt).local().format('LLLL') : msg.createdAt;

    div.innerHTML = `
        ${isMine ? getActionsHtml(msg.id) : ''}
        <div class="msg-content-wrapper">
            ${replyHtml}
            <div class="msg-bubble ${isCallMsg ? 'p-0 bg-transparent shadow-none' : ''}" title="${timeTooltip}">
                ${contentHtml}
            </div>
        </div>
        ${!isMine ? getActionsHtml(msg.id) : ''}
    `;

    body.appendChild(div);
}

function sendMainChat() {
    const input = document.getElementById('main-chat-input');
    const msg = input.value.trim();
    if (!msg) return;

    const replyIdInt = replyingToId ? parseInt(replyingToId) : null;
    connection.invoke("SendMessage", window.currentReceiverId, msg, "Text", replyIdInt)
        .catch(err => console.error(err));

    input.value = '';
    input.focus();
}

function uploadFile(input) {
    if (input.files.length === 0) return;
    var formData = new FormData();
    formData.append("file", input.files[0]);

    fetch('/Chat/UploadChatFile', { method: 'POST', body: formData })
        .then(res => res.json())
        .then(res => {
            if (res.success) {
                const replyIdInt = replyingToId ? parseInt(replyingToId) : null;
                connection.invoke("SendMessage", window.currentReceiverId, res.url, res.type, replyIdInt);
                cancelReply();
            }
        });
}

function startReply(msgId) {
    const msgEl = document.getElementById(`msg-${msgId}`);
    if (msgEl) {
        const bubble = msgEl.querySelector('.msg-bubble');
        const msgContent = bubble.querySelector('img') ? '[Hình ảnh]' : bubble.querySelector('video') ? '[Video]' : bubble.innerText;
        replyingToId = msgId;
        const replyBar = document.getElementById('reply-bar');
        replyBar.style.display = 'flex';
        document.getElementById('reply-content').innerText = msgContent;
        document.getElementById('main-chat-input').focus();
    }
}

function cancelReply() {
    replyingToId = null;
    document.getElementById('reply-bar').style.display = 'none';
}

function deleteMsg(msgId, mode) {
    connection.invoke("DeleteMessage", parseInt(msgId), mode);
}

function scrollMainToBottom() {
    const body = document.getElementById('main-chat-body');
    if (body) body.scrollTop = body.scrollHeight;
}

function updateVideoUI(name, avatar) {
    const nameEl = document.getElementById('video-name');
    const avtEl = document.getElementById('video-avatar');
    if (nameEl) nameEl.innerText = name;
    if (avtEl) avtEl.src = avatar;
}

async function startCall() {
    if (!window.currentReceiverId) { alert("Chưa chọn người!"); return; }
    window.currentChattingUserId = window.currentReceiverId;
    isCaller = true;

    const name = document.getElementById('header-name').innerText;
    const avatar = document.getElementById('header-avatar').src;
    updateVideoUI(name, avatar);

    await startLocalVideo();
    updateCallStatus("Đang đổ chuông...");
    connection.invoke("CallUser", window.currentReceiverId);

}
async function startCallFromMiniChat(userId) {
    window.currentChattingUserId = userId;
    isCaller = true;

    const chatBox = document.getElementById(`chat-box-${userId}`);
    if (chatBox) {
        const name = chatBox.querySelector('.chat-username').innerText;
        const avatar = chatBox.querySelector('.chat-avatar').src;
        updateVideoUI(name, avatar);
    }

    await startLocalVideo();
    updateCallStatus("Đang đổ chuông...");
    connection.invoke("CallUser", userId);
}

async function acceptIncomingCall() {
    const ringtone = document.getElementById('ringtone');
    if (ringtone) { ringtone.pause(); ringtone.currentTime = 0; }

    const modalEl = document.getElementById('incomingCallModal');
    if (modalEl) {
        if (typeof bootstrap !== 'undefined') { const m = bootstrap.Modal.getInstance(modalEl); if (m) m.hide(); }
        modalEl.classList.remove('show');
        modalEl.style.display = 'none';
        const backdrop = document.querySelector('.modal-backdrop');
        if (backdrop) backdrop.remove();
    }

    isCaller = false;

    updateVideoUI(window.incomingCallerName, window.incomingCallerAvatar);

    await startLocalVideo();
    updateCallStatus("Đang kết nối...");
    connection.invoke("AnswerCall", window.incomingCallerId);
}
function endCall(notifyServer = true) {
    const targetId = isCaller ? (window.currentChattingUserId || window.currentReceiverId) : window.incomingCallerId;
    let duration = 0;
    let status = "ended";

    if (callStartTime) {
        duration = Math.floor((new Date() - callStartTime) / 1000);
    } else {
        status = "missed";
    }

    stopTimer();
    closeConnection();

    if (notifyServer && targetId) {
        connection.invoke("EndCall", targetId);
        connection.invoke("SaveCallHistory", targetId, status, duration);
    }
}

async function startLocalVideo() {
    try {
        localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        const lv = document.getElementById('local-video');
        if (lv) { lv.srcObject = localStream; lv.muted = true; }
        const m = document.getElementById('video-call-modal');
        if (m) { m.classList.remove('d-none'); m.classList.add('d-flex'); }
    } catch (e) {
        console.error(e);
        alert("Không thể truy cập Camera. Vui lòng kiểm tra quyền hoặc kết nối HTTPS.");
        endCall(false);
    }
}

function createPeerConnection(targetUserId) {
    peerConnection = new RTCPeerConnection(rtcConfig);
    if (localStream) localStream.getTracks().forEach(t => peerConnection.addTrack(t, localStream));
    peerConnection.ontrack = e => {
        const rv = document.getElementById('remote-video');
        if (rv && rv.srcObject !== e.streams[0]) { rv.srcObject = e.streams[0]; startTimer(); }
    };
    peerConnection.onicecandidate = e => {
        if (e.candidate) connection.invoke("SendSignal", targetUserId, JSON.stringify({ type: "candidate", candidate: e.candidate }));
    };
}

function closeConnection() {
    if (localStream) { localStream.getTracks().forEach(t => t.stop()); localStream = null; }
    if (peerConnection) { peerConnection.close(); peerConnection = null; }
    const m = document.getElementById('video-call-modal');
    if (m) { m.classList.add('d-none'); m.classList.remove('d-flex'); }
    const rv = document.getElementById('remote-video');
    if (rv) rv.srcObject = null;
}

function updateCallStatus(text) { const el = document.getElementById('call-timer'); if (el) el.innerText = text; }
function startTimer() {
    if (callTimerInterval) clearInterval(callTimerInterval);
    callStartTime = new Date();
    const el = document.getElementById('call-timer');
    callTimerInterval = setInterval(() => {
        const diff = Math.floor((new Date() - callStartTime) / 1000);
        const m = Math.floor(diff / 60).toString().padStart(2, '0');
        const s = (diff % 60).toString().padStart(2, '0');
        if (el) el.innerText = `${m}:${s}`;
    }, 1000);
}
function stopTimer() { if (callTimerInterval) { clearInterval(callTimerInterval); callTimerInterval = null; } callStartTime = null; }
function toggleMute() { if (localStream) { const t = localStream.getAudioTracks()[0]; if (t) t.enabled = !t.enabled; } }
function toggleCam() { if (localStream) { const t = localStream.getVideoTracks()[0]; if (t) t.enabled = !t.enabled; } }

function playMsgSound() { var a = document.getElementById("msg-sound"); if (a) a.play().catch(e => { }); }
function updateMsgBadge() { var b = document.getElementById("msg-badge"); if (b) { var c = parseInt(b.innerText) || 0; b.innerText = (c + 1) > 9 ? "9+" : (c + 1); b.classList.remove("d-none"); } }

function openChat(userId, fullName, avatarUrl) {
    if (document.getElementById(`chat-box-${userId}`)) return;
    var t = document.getElementById('chat-box-template'); if (!t) return;
    var c = t.content.cloneNode(true);
    var d = c.querySelector('.chat-box');
    d.id = `chat-box-${userId}`; d.dataset.userId = userId;
    d.querySelector('.chat-username').innerText = fullName;
    d.querySelector('.chat-avatar').src = avatarUrl || '/images/default-avatar.png';
    d.querySelector('.btn-close-chat').onclick = function () { d.remove(); };
    d.querySelector('.btn-minimize').onclick = function () {
        var b = d.querySelector('.chat-body'); var f = d.querySelector('.chat-footer');
        if (b.style.display === 'none') { b.style.display = 'flex'; f.style.display = 'block'; d.style.height = '450px'; } else { b.style.display = 'none'; f.style.display = 'none'; d.style.height = 'auto'; }
    };
    d.querySelector('.btn-send').onclick = function () { sendMsg(userId, d); };
    d.querySelector('.chat-input').addEventListener("keyup", function (e) { if (e.key === "Enter") sendMsg(userId, d); });
    var btnV = d.querySelector('.btn-video-call'); if (btnV) btnV.onclick = function () { startCallFromMiniChat(userId); };
    document.getElementById('chat-container').appendChild(d);
    loadConversation(userId, d.querySelector('.chat-body'));
}
function sendMsg(rId, div) {
    var i = div.querySelector('.chat-input'); var m = i.value.trim(); if (m === "") return;
    connection.invoke("SendMessage", rId, m, "Text", null).catch(e => console.error(e)); i.value = ''; i.focus();
}
function appendMessage(con, txt, type) { var d = document.createElement('div'); d.className = `chat-message msg-${type}`; d.innerText = txt; con.appendChild(d); }
function scrollToBottom(el) { el.scrollTop = el.scrollHeight; }

var mainInput = document.getElementById('main-chat-input');
if (mainInput) {
    mainInput.addEventListener("keyup", function (e) {
        if (e.key === "Enter") sendMainChat();
    });
}

window.addEventListener('beforeunload', function (e) {
    if (peerConnection || localStream) {
        endCall(true);
    }
}); 