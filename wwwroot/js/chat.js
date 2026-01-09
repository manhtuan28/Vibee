"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/chatHub").build();

connection.start().then(function () {
    console.log("SignalR Connected!");
}).catch(function (err) {
    return console.error(err.toString());
});

connection.on("ReceiveMessage", function (senderId, content, createdAt, messageId) {
    var chatBox = document.getElementById(`chat-box-${senderId}`);

    var isInFullChat = (typeof currentReceiverId !== 'undefined' && currentReceiverId === senderId);

    if (chatBox) {
        var body = chatBox.querySelector('.chat-body');
        appendMessage(body, content, 'theirs');
        scrollToBottom(body);
    }
    else if (isInFullChat) {
    }
    else {
        console.log("Có tin nhắn mới từ: " + senderId);

        var audio = document.getElementById("msg-sound");
        if (audio) {
            audio.play().catch(err => console.log("Không thể phát âm thanh (User chưa tương tác): " + err));
        }

        var badge = document.getElementById("msg-badge");
        var btn = document.getElementById("btn-msg-nav");

        if (badge) {
            var currentCount = parseInt(badge.innerText) || 0;
            var newCount = currentCount + 1;

            badge.innerText = newCount > 9 ? "9+" : newCount;
            badge.classList.remove("d-none");

            if (btn) {
                btn.classList.add("animate__animated", "animate__swing");
                setTimeout(() => btn.classList.remove("animate__animated", "animate__swing"), 1000);
            }
        }
    }
});

connection.on("MessageSent", function (receiverId, content, createdAt, messageId) {
    var chatBox = document.getElementById(`chat-box-${receiverId}`);
    if (chatBox) {
        var body = chatBox.querySelector('.chat-body');
        appendMessage(body, content, 'mine');
        scrollToBottom(body);

        var input = chatBox.querySelector('.chat-input');
        input.value = '';
        input.focus();
    }
});

var btnMsg = document.getElementById("btn-msg-nav");
if (btnMsg) {
    btnMsg.addEventListener("click", function () {
        var badge = document.getElementById("msg-badge");
        if (badge) {
            badge.innerText = "0";
            badge.classList.add("d-none");
        }
    });
}


function openChat(userId, fullName, avatarUrl) {
    if (document.getElementById(`chat-box-${userId}`)) {
        return;
    }

    var template = document.getElementById('chat-box-template');
    var clone = template.content.cloneNode(true);
    var chatDiv = clone.querySelector('.chat-box');

    chatDiv.id = `chat-box-${userId}`;
    chatDiv.querySelector('.chat-username').innerText = fullName;
    chatDiv.querySelector('.chat-avatar').src = avatarUrl || '/images/default-avatar.png';

    chatDiv.querySelector('.btn-close-chat').onclick = function () {
        chatDiv.remove();
    };
    chatDiv.querySelector('.btn-minimize').onclick = function () {
        var body = chatDiv.querySelector('.chat-body');
        var footer = chatDiv.querySelector('.chat-footer');
        if (body.style.display === 'none') {
            body.style.display = 'flex';
            footer.style.display = 'block';
            chatDiv.style.height = '450px';
        } else {
            body.style.display = 'none';
            footer.style.display = 'none';
            chatDiv.style.height = 'auto';
        }
    };
    chatDiv.querySelector('.btn-send').onclick = function () {
        sendMsg(userId, chatDiv);
    };
    chatDiv.querySelector('.chat-input').addEventListener("keyup", function (event) {
        if (event.key === "Enter") {
            sendMsg(userId, chatDiv);
        }
    });

    document.getElementById('chat-container').appendChild(chatDiv);

    loadConversation(userId, chatDiv.querySelector('.chat-body'));
}

function sendMsg(receiverId, chatDiv) {
    var input = chatDiv.querySelector('.chat-input');
    var message = input.value.trim();
    if (message === "") return;

    connection.invoke("SendMessage", receiverId, message).catch(function (err) {
        return console.error(err.toString());
    });
}

function loadConversation(otherUserId, chatBody) {
    fetch(`/Message/GetConversation/${otherUserId}`)
        .then(response => response.json())
        .then(res => {
            if (res.success) {
                chatBody.innerHTML = '';
                res.data.forEach(msg => {
                    var type = msg.isMine ? 'mine' : 'theirs';
                    appendMessage(chatBody, msg.content, type);
                });
                scrollToBottom(chatBody);
            }
        })
        .catch(err => console.error(err));
}

function appendMessage(container, content, type) {
    var div = document.createElement('div');
    div.classList.add('chat-message', `msg-${type}`);
    div.innerText = content;
    container.appendChild(div);
}

function scrollToBottom(element) {
    element.scrollTop = element.scrollHeight;
}