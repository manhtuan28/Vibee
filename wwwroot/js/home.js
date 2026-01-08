document.addEventListener("DOMContentLoaded", function () {
    setTimeout(initFeedVideos, 500);
});

function initFeedVideos() {
    var videos = document.querySelectorAll('.video-js');
    videos.forEach(function (videoTag) {
        if (!videojs.getPlayer(videoTag.id)) {
            var player = videojs(videoTag.id);
            player.on('click', function (e) {
                if (e.target.closest('.vjs-control-bar')) return;

                if (e.target.closest('.vjs-big-play-button')) return;

                e.preventDefault();

                if (player.paused()) {
                    document.querySelectorAll('video').forEach(v => {
                        if (v !== player.tech().el() && !v.paused) v.pause();
                    });
                    player.play();
                } else {
                    player.pause();
                }
            });
        }
    });
}

let uploadedFiles = [];
let currentStatus = { emotionHTML: '', locationName: '' };
let editNewFiles = [];
let removedMediaIds = [];
let commentPagination = {};
let commentOffsets = {};
let currentMediaList = [];
let currentMediaIndex = 0;
let currentPlayer = null;

function openPostModal(view = 'default') {
    uploadedFiles = [];
    currentStatus = { emotionHTML: '', locationName: '' };
    updateHeaderStatus();
    clearAllMedia();
    document.getElementById('createPostForm').reset();
    setPrivacy(0, 'Công khai', 'bi-globe-americas');
    var myModal = new bootstrap.Modal(document.getElementById('createPostModal'));
    myModal.show();
    switchView(view);
}

function setPrivacy(level, text, iconClass) {
    document.getElementById('hiddenPrivacy').value = level;
    document.getElementById('privacyText').innerText = text;
    document.getElementById('privacyIcon').className = `bi ${iconClass} me-1`;
}

function deletePost(id) {
    Swal.fire({
        title: 'Xóa bài viết?', text: "Không thể hoàn tác!", icon: 'warning', showCancelButton: true, confirmButtonColor: '#dc3545', confirmButtonText: 'Xóa', cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.showLoading();
            $.post('/Posts/Delete', { id: id }, function (res) {
                Swal.close();
                if (res.success) {
                    $(`#post-${id}`).fadeOut(500, function () { $(this).remove(); });
                    Swal.mixin({ toast: true, position: 'bottom-start', showConfirmButton: false, timer: 3000 }).fire({ icon: 'success', title: 'Đã xóa' });
                } else { Swal.fire('Lỗi!', res.message, 'error'); }
            });
        }
    })
}

function switchView(viewName) {
    document.querySelectorAll('.modal-view').forEach(el => el.classList.remove('active'));
    document.getElementById('view-' + viewName).classList.add('active');
    const title = document.getElementById('modalTitle');
    const btnBack = document.getElementById('btnBackView');
    if (viewName === 'default') { title.innerText = 'Tạo bài viết'; btnBack.classList.add('d-none'); }
    else if (viewName === 'feeling') { title.innerText = 'Cảm xúc'; btnBack.classList.remove('d-none'); }
    else if (viewName === 'location') { title.innerText = 'Tìm kiếm vị trí'; btnBack.classList.remove('d-none'); document.getElementById('searchLocInput').focus(); }
}

function selectFeeling(icon, text) {
    currentStatus.emotionHTML = `<span class="fw-normal">${icon} ${text}</span>`;
    document.getElementById('hiddenEmotion').value = currentStatus.emotionHTML; updateHeaderStatus(); switchView('default');
}
function selectLocation(locName) {
    currentStatus.locationName = locName;
    document.getElementById('hiddenLocation').value = locName; updateHeaderStatus(); switchView('default');
}
function removeStatus(type) {
    if (type === 'emotion') { currentStatus.emotionHTML = ''; document.getElementById('hiddenEmotion').value = ''; }
    else if (type === 'location') { currentStatus.locationName = ''; document.getElementById('hiddenLocation').value = ''; }
    updateHeaderStatus();
}
function updateHeaderStatus() {
    const area = document.getElementById('statusArea');
    let html = '';
    if (currentStatus.emotionHTML) html += `${currentStatus.emotionHTML} <i class="bi bi-x-circle-fill text-secondary ms-1 cursor-pointer" onclick="removeStatus('emotion')" style="font-size:12px"></i>`;
    if (currentStatus.locationName) {
        if (html) html += ' '; html += `tại <strong>${currentStatus.locationName}</strong> <i class="bi bi-x-circle-fill text-secondary ms-1 cursor-pointer" onclick="removeStatus('location')" style="font-size:12px"></i>`;
    }
    area.innerHTML = html; checkInput();
}

let debounceTimer;
document.getElementById('searchLocInput').addEventListener('input', function (e) {
    clearTimeout(debounceTimer); const query = e.target.value.trim(); const list = document.getElementById('locationList');
    if (query.length < 2) { list.innerHTML = ''; return; }
    document.getElementById('locationLoading').classList.remove('d-none');
    debounceTimer = setTimeout(() => {
        fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${query}&addressdetails=1&limit=5`)
            .then(res => res.json()).then(data => {
                document.getElementById('locationLoading').classList.add('d-none');
                let html = '';
                data.forEach(p => {
                    let name = p.name || p.address.city || "";
                    html += `<div class="item-list-row px-3 py-2 border-bottom" onclick="selectLocation('${name}')"><div class="emoji-icon bg-secondary bg-opacity-10 me-2 rounded-circle d-flex align-items-center justify-content-center" style="width:40px;height:40px"><i class="bi bi-geo-alt-fill text-danger"></i></div><div><div class="fw-bold text-truncate">${name}</div><small class="text-muted">${p.display_name}</small></div></div>`;
                });
                list.innerHTML = html;
            });
    }, 500);
});

function handleFilesSelect(input) { if (input.files) Array.from(input.files).forEach(f => uploadedFiles.push(f)); renderPreview(); checkInput(); input.value = ''; }
function removeFile(i) { uploadedFiles.splice(i, 1); renderPreview(); checkInput(); }
function clearAllMedia() { uploadedFiles = []; renderPreview(); }
function renderPreview() {
    const c = document.getElementById('modalMediaPreview');
    const w = document.getElementById('mediaPreviewContainer'); c.innerHTML = '';
    if (uploadedFiles.length === 0) { w.classList.add('d-none'); return; } w.classList.remove('d-none');
    uploadedFiles.forEach((f, i) => {
        const reader = new FileReader();
        reader.onload = function (e) {
            const colClass = uploadedFiles.length === 1 ? 'col-12' : 'col-6';
            let media = f.type.startsWith('video') ? `<video src="${e.target.result}" class="rounded border w-100 h-100 object-fit-cover"></video>` : `<img src="${e.target.result}" class="rounded border w-100 h-100 object-fit-cover">`;
            c.innerHTML += `<div class="${colClass} position-relative"><div class="ratio ratio-4x3">${media}</div><div class="media-remove-btn" onclick="removeFile(${i})"><i class="bi bi-x"></i></div></div>`;
        }
        reader.readAsDataURL(f);
    });
}

const contentInput = document.getElementById('modalPostContent');
const submitBtn = document.getElementById('btnSubmitPost');
function checkInput() {
    const hasText = contentInput.value.trim().length > 0;
    const hasFile = uploadedFiles.length > 0;
    const hasStatus = currentStatus.emotionHTML !== '' || currentStatus.locationName !== '';
    if (hasText || hasFile || hasStatus) submitBtn.classList.remove('disabled'); else submitBtn.classList.add('disabled');
}
contentInput.addEventListener('input', checkInput);

function submitPostModal() {
    var btn = document.getElementById('btnSubmitPost'); btn.classList.add('btn-loading');
    var fd = new FormData(document.getElementById('createPostForm'));
    uploadedFiles.forEach(f => fd.append('mediaFiles', f));
    setTimeout(() => {
        $.ajax({
            url: '/Posts/CreatePost', type: 'POST', data: fd, processData: false, contentType: false,
            success: function (res) {
                if (res.success) {
                    var modal = bootstrap.Modal.getInstance(document.getElementById('createPostModal')); modal.hide();
                    Swal.fire({ icon: 'success', title: 'Đã đăng!', showConfirmButton: false, timer: 1000 }).then(() => location.reload());
                } else { Swal.fire('Lỗi', res.message, 'error'); btn.classList.remove('btn-loading'); }
            },
            error: function () { Swal.fire('Lỗi', 'Không thể kết nối', 'error'); btn.classList.remove('btn-loading'); }
        });
    }, 800);
}
function openEditModal(id) {
    editNewFiles = [];
    removedMediaIds = []; document.getElementById('editRemovedMediaIds').value = '';
    document.getElementById('newMediaPreview').innerHTML = ''; document.getElementById('oldMediaPreview').innerHTML = '';
    Swal.showLoading();
    $.get('/Posts/GetPost?id=' + id, function (res) {
        Swal.close();
        if (res.success) {
            const data = res.data;
            document.getElementById('editPostId').value = data.id;
            document.getElementById('editContent').value = data.content;
            document.getElementById('editHiddenLocation').value = data.locationName || '';
            document.getElementById('editHiddenEmotion').value = data.emotion || '';
            let privText = data.privacyLevel == 0 ? 'Công khai' : (data.privacyLevel == 1 ? 'Bạn bè' : 'Chỉ mình tôi');
            let privIcon = data.privacyLevel == 0 ? 'bi-globe-americas' : (data.privacyLevel == 1 ? 'bi-people-fill' : 'bi-lock-fill');
            setEditPrivacy(data.privacyLevel, privText, privIcon);
            updateEditHeaderStatus(data.emotion, data.locationName);
            const oldContainer = document.getElementById('oldMediaPreview');
            if (data.medias && data.medias.length > 0) {
                data.medias.forEach(m => {
                    let mediaHtml = m.type === 1 ? `<video src="${m.url}" class="rounded border w-100 h-100 object-fit-cover"></video>` : `<img src="${m.url}" class="rounded border w-100 h-100 object-fit-cover">`;
                    oldContainer.innerHTML += `<div class="col-4 position-relative" id="old-media-${m.id}"><div class="ratio ratio-4x3">${mediaHtml}</div><div class="media-remove-btn" onclick="removeOldMedia(${m.id})"><i class="bi bi-x"></i></div></div>`;
                });
                document.getElementById('editMediaContainer').classList.remove('d-none');
            } else { document.getElementById('editMediaContainer').classList.add('d-none'); }
            new bootstrap.Modal(document.getElementById('editPostModal')).show();
        }
    });
}
function setEditPrivacy(l, t, i) { document.getElementById('editHiddenPrivacy').value = l; document.getElementById('editPrivacyText').innerText = t; document.getElementById('editPrivacyIcon').className = `bi ${i} me-1`; }
function updateEditHeaderStatus(e, l) { let h = ''; if (e) h += `${e} `; if (l) h += `tại <strong>${l}</strong>`; document.getElementById('editStatusArea').innerHTML = h; }
function clearEditStatus() { document.getElementById('editHiddenLocation').value = ''; document.getElementById('editHiddenEmotion').value = ''; updateEditHeaderStatus('', ''); }
function removeOldMedia(id) { removedMediaIds.push(id); document.getElementById('editRemovedMediaIds').value = removedMediaIds.join(','); document.getElementById(`old-media-${id}`).remove(); }
function handleEditFiles(input) { if (input.files) { Array.from(input.files).forEach(f => editNewFiles.push(f)); renderEditNewPreview(); } input.value = ''; document.getElementById('editMediaContainer').classList.remove('d-none'); }
function renderEditNewPreview() {
    const container = document.getElementById('newMediaPreview'); container.innerHTML = '';
    editNewFiles.forEach((f, i) => {
        const reader = new FileReader();
        reader.onload = function (e) {
            let mediaHtml = f.type.startsWith('video') ? `<video src="${e.target.result}" class="rounded border w-100 h-100 object-fit-cover"></video>` : `<img src="${e.target.result}" class="rounded border w-100 h-100 object-fit-cover">`;
            container.innerHTML += `<div class="col-4 position-relative"><div class="ratio ratio-4x3">${mediaHtml}</div><div class="media-remove-btn" onclick="removeNewEditFile(${i})"><i class="bi bi-x"></i></div></div>`;
        }
        reader.readAsDataURL(f);
    });
}
function removeNewEditFile(i) { editNewFiles.splice(i, 1); renderEditNewPreview(); }
function submitEditPost() {
    var btn = document.getElementById('btnSaveEdit'); btn.classList.add('btn-loading');
    var fd = new FormData(document.getElementById('editPostForm')); editNewFiles.forEach(f => fd.append('newMediaFiles', f));
    $.ajax({
        url: '/Posts/Edit', type: 'POST', data: fd, processData: false, contentType: false,
        success: function (res) {
            if (res.success) { bootstrap.Modal.getInstance(document.getElementById('editPostModal')).hide(); Swal.fire({ icon: 'success', title: 'Đã cập nhật!', showConfirmButton: false, timer: 1000 }).then(() => location.reload()); }
            else { Swal.fire('Lỗi', res.message, 'error'); btn.classList.remove('disabled'); btn.classList.remove('btn-loading'); }
        }
    });
}
function doReact(postId, type) {
    $.post('/Interactions/React', { postId: postId, type: type }, function (res) {
        if (res.success) {
            const btn = $(`#btn-react-${postId}`);
            const spanLbl = btn.find('span');
            const iconBtn = btn.find('i');

            btn.removeClass('btn-like-active btn-love-active btn-haha-active btn-wow-active btn-sad-active btn-angry-active');
            iconBtn.removeClass().addClass('bi me-1');

            const reactConfigs = {
                'Like': { class: 'btn-like-active', icon: 'bi-hand-thumbs-up-fill', label: 'Thích' },
                'Love': { class: 'btn-love-active', icon: 'bi-heart-fill', label: 'Yêu thích' },
                'Haha': { class: 'btn-haha-active', icon: 'bi-emoji-laughing-fill', label: 'Haha' },
                'Wow': { class: 'btn-wow-active', icon: 'bi-emoji-surprise-fill', label: 'Wow' },
                'Sad': { class: 'btn-sad-active', icon: 'bi-emoji-frown-fill', label: 'Buồn' },
                'Angry': { class: 'btn-angry-active', icon: 'bi-emoji-angry-fill', label: 'Phẫn nộ' }
            };

            const svgIcons = {
                'Like': `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='8' cy='8' r='8' fill='#1877F2'/><path d='M8.5 4h1a1.5 1.5 0 0 1 1.5 1.5v4a1.5 1.5 0 0 1-1.5 1.5h-1s1 2.5-1.5 3c-1.8.4-1.5-1.5-2.5-2H4V5h1.1c.9 0 1.9-2 3.4-1z' fill='#fff'/></svg>`,
                'Love': `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='8' cy='8' r='8' fill='#F3425F'/><path d='M8 12.5s-4.5-2.5-4.5-5.5c0-1.5 1-2.5 2.5-2.5 1 0 2 .5 2 1.5 0-1 1-1.5 2-1.5 1.5 0 2.5 1 2.5 2.5C12.5 10 8 12.5 8 12.5z' fill='#fff'/></svg>`,
                'Haha': `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='8' cy='8' r='8' fill='#F7B125'/><path d='M4.5 9.5a3.5 3.5 0 0 0 7 0c0-.5-.5-1-1-1h-5c-.5 0-1 .5-1 1z' fill='#4B3212'/><path d='M5.5 6a1 1 0 1 1 0-1.5 1 1 0 0 1 0 1.5zm5 0a1 1 0 1 1 0-1.5 1 1 0 0 1 0 1.5z' fill='#4B3212'/><path d='M4.5 9.5a3.5 3.5 0 0 0 7 0' fill='none' stroke='#4B3212' stroke-width='0.5'/></svg>`,
                'Wow': `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='8' cy='8' r='8' fill='#F7B125'/><ellipse cx='8' cy='10' rx='2' ry='3' fill='#4B3212'/><path d='M5.5 6a1 1 0 1 1 0-1.5 1 1 0 0 1 0 1.5zm5 0a1 1 0 1 1 0-1.5 1 1 0 0 1 0 1.5z' fill='#4B3212'/></svg>`,
                'Sad': `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='8' cy='8' r='8' fill='#F7B125'/><path d='M5.5 10.5c.5-.5 1.5-1 2.5-1s2 .5 2.5 1' fill='none' stroke='#4B3212' stroke-width='1'/><path d='M5.5 6a1 1 0 1 1 0-1.5 1 1 0 0 1 0 1.5zm5 0a1 1 0 1 1 0-1.5 1 1 0 0 1 0 1.5z' fill='#4B3212'/><circle cx='8' cy='6' r='0.5' fill='#4B3212'/></svg>`,
                'Angry': `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='8' cy='8' r='8' fill='#E4605A'/><path d='M5.5 10c.5-.5 1.5-1 2.5-1s2 .5 2.5 1' fill='none' stroke='#fff' stroke-width='1'/><path d='M5 5.5l1.5 1m3 0l1.5-1' stroke='#4B0000' stroke-width='1' stroke-linecap='round'/><circle cx='6' cy='7' r='0.8' fill='#4B0000'/><circle cx='10' cy='7' r='0.8' fill='#4B0000'/></svg>`
            };

            if (res.action === 'removed') {
                iconBtn.addClass('bi-hand-thumbs-up');
                spanLbl.text('Thích');
            } else {
                const config = reactConfigs[type];
                btn.addClass(config.class);
                iconBtn.addClass(config.icon);
                spanLbl.text(config.label);
            }

            const countArea = $(`#count-react-${postId}`);
            let countSpan = countArea.find('span[data-count]');

            if (countSpan.length === 0) {
                countArea.html(`
                    <div class="reaction-stack" style="display:none"></div>
                    <span class="hover-underline cursor-pointer text-muted small" data-count="0"></span>
                `);
                countSpan = countArea.find('span[data-count]');
            }

            let currentCount = parseInt(countSpan.attr('data-count') || 0);

            if (res.action === 'added') currentCount++;
            else if (res.action === 'removed') currentCount--;

            countSpan.attr('data-count', currentCount);

            const stackDiv = countArea.find('.reaction-stack');

            if (currentCount > 0) {
                if (res.action !== 'removed') {
                    stackDiv.find(`[data-type="${type}"]`).remove();

                    const newIconHtml = `<div class="reaction-icon-img" data-type="${type}">${svgIcons[type]}</div>`;
                    stackDiv.prepend(newIconHtml);

                    const icons = stackDiv.find('.reaction-icon-img');
                    if (icons.length > 2) {
                        icons.last().remove();
                    }

                    stackDiv.show();
                }

                countSpan.text(currentCount);
            } else {
                stackDiv.empty().hide();
                countSpan.text('');
            }
        }
    });
}
function toggleComment(postId) {
    const box = $(`#comment-box-${postId}`);
    if (box.hasClass('d-none')) {
        box.removeClass('d-none');
        if ($(`#comment-list-${postId}`).children().length === 0) {
            commentOffsets[postId] = 0;
            loadComments(postId);
        }
        $(`#input-comment-${postId}`).focus();
    } else {
        box.addClass('d-none');
    }
}
function loadComments(postId) {
    const list = $(`#comment-list-${postId}`);
    const btnMoreId = `btn-more-${postId}`;

    let skip = commentOffsets[postId] || 0;
    let take = skip === 0 ? 5 : 10;

    $(`#${btnMoreId}`).remove();
    if (skip === 0) list.html('<div class="text-center small text-muted my-2"><span class="spinner-border spinner-border-sm"></span> Đang tải...</div>');

    $.get(`/Interactions/GetComments?postId=${postId}&skip=${skip}&take=${take}`, function (res) {
        if (skip === 0) list.empty();

        if (res.success) {
            commentOffsets[postId] = skip + res.data.length;

            res.data.forEach(c => {
                let repliesHtml = '';
                if (c.replies && c.replies.length > 0) {
                    c.replies.forEach(r => {
                        repliesHtml += renderCommentHtml(r, true);
                    });
                }

                const commentHtml = renderCommentHtml(c, false, repliesHtml);
                list.append(commentHtml);
            });

            if (res.hasMore) {
                list.append(`<button id="${btnMoreId}" class="btn-load-more ps-2" onclick="loadComments(${postId})">Xem thêm bình luận...</button>`);
            }
        }
    });
}

function renderCommentHtml(c, isReply, childHtml = '') {
    const size = isReply ? 24 : 32;

    const deleteBtn = c.isOwner
        ? `<span class="action-link text-delete" onclick="deleteComment(${c.id})">Xóa</span>`
        : '';

    const targetId = isReply ? c.parentId : c.id;
    const replyBtn = `<span class="action-link" onclick="showReplyBox(${targetId}, '${c.userName}')">Phản hồi</span>`;

    return `
    <div class="comment-item" id="cmt-${c.id}">
        <div class="d-flex align-items-start gap-2">
            <a href="/Profile/${c.userParams}">
                <img src="${c.avatar}" class="rounded-circle border" width="${size}" height="${size}" style="object-fit:cover">
            </a>
            <div class="flex-grow-1">
                <div class="comment-bubble">
                    <a href="/Profile/${c.userParams}" class="fw-bold text-decoration-none text-dark small">${c.userName}</a>
                    <div class="small text-break">${c.content}</div>
                </div>
                <div class="comment-actions">
                    <span>${c.time}</span>
                    <span class="action-link fw-bold">Thích</span>
                    ${replyBtn}
                    ${deleteBtn}
                </div>
            </div>
        </div>
        
        ${!isReply ? `
            <div id="reply-list-${c.id}" class="reply-list ${childHtml ? '' : 'd-none'}">
                ${childHtml}
            </div>
            <div id="reply-input-container-${c.id}" class="reply-list d-none"></div>
        ` : ''}
    </div>`;
}

function renderSingleCommentHtml(c, isReply, childHtml = '') {
    const marginClass = isReply ? 'mb-1' : 'mb-2';
    const sizeClass = isReply ? 'width="24" height="24"' : 'width="32" height="32"';

    let deleteBtn = c.isOwner ? `<span class="comment-action-btn text-delete" onclick="deleteComment(${c.id}, ${isReply ? c.parentId : c.id}, ${isReply})">Xóa</span>` : '';

    let replyTargetId = isReply ? c.parentId : c.id;
    let replyBtn = `<span class="comment-action-btn" onclick="showReplyBox(${replyTargetId}, '${c.userName}')">Phản hồi</span>`;

    return `
    <div class="comment-item ${marginClass}" id="cmt-${c.id}">
        <div class="d-flex align-items-start gap-2">
            <a href="/Profile/${c.userParams}">
                <img src="${c.avatar}" class="rounded-circle border" ${sizeClass} style="object-fit:cover">
            </a>
            <div class="flex-grow-1">
                <div class="comment-bubble">
                    <a href="/Profile/${c.userParams}" class="fw-bold text-decoration-none text-dark small">${c.userName}</a>
                    <div class="small text-break">${c.content}</div>
                </div>
                <div class="comment-actions">
                    <span>${c.time}</span>
                    <span class="comment-action-btn font-weight-bold">Thích</span>
                    ${replyBtn}
                    ${deleteBtn}
                </div>
            </div>
        </div>
        <div id="reply-list-${c.id}" class="reply-list ${childHtml ? '' : 'd-none'}">
            ${childHtml}
        </div>
        <div id="reply-input-container-${c.id}" class="reply-input-box d-none"></div>
    </div>`;
}

function sendComment(postId, parentId = null) {
    const inputId = parentId ? `#input-reply-${parentId}` : `#input-comment-${postId}`;
    const input = $(inputId);
    const content = input.val().trim();
    if (!content) return;

    $.post('/Interactions/Comment', { postId: postId, content: content, parentId: parentId }, function (res) {
        if (res.success) {
            input.val('');

            const newHtml = renderCommentHtml(res, !!parentId);

            if (parentId) {
                const list = $(`#reply-list-${parentId}`);
                list.removeClass('d-none');
                list.append(newHtml);
                $(`#reply-input-container-${parentId}`).addClass('d-none');
            } else {
                $(`#comment-list-${postId}`).prepend(newHtml);
                if (commentOffsets[postId]) commentOffsets[postId]++;
            }
        }
    });
}
function sharePost(postId) {
    Swal.fire({
        title: 'Chia sẻ bài viết?',
        input: 'text',
        inputPlaceholder: 'Nói gì đó về bài viết này...',
        showCancelButton: true,
        confirmButtonText: 'Chia sẻ ngay',
        cancelButtonText: 'Hủy',
        confirmButtonColor: '#1877f2'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.showLoading();

            $.post('/Interactions/Share', { postId: postId, content: result.value }, function (res) {
                Swal.close();
                if (res.success) {
                    const shareSpan = $(`#share-count-${postId}`);
                    shareSpan.html(`${res.newShareCount} lượt chia sẻ`);

                    Swal.fire({
                        icon: 'success',
                        title: 'Đã chia sẻ!',
                        text: 'Bài viết đã được đưa lên tường nhà bạn.',
                        showConfirmButton: false,
                        timer: 1500
                    });
                } else {
                    Swal.fire('Lỗi', 'Không thể chia sẻ bài viết này', 'error');
                }
            });
        }
    });
}
function showReplyBox(parentId, userName) {
    const container = $(`#reply-input-container-${parentId}`);

    if (container.html().trim() === '') {
        const postId = $(container).closest('.fb-card').attr('id').replace('post-', '');

        const html = `
            <div class="d-flex align-items-center gap-2 mt-1 mb-2">
                <img src="${currentUserAvatar}" class="rounded-circle" width="24" height="24" style="object-fit:cover">
                <div class="position-relative flex-grow-1">
                    <input id="input-reply-${parentId}" class="form-control form-control-sm rounded-pill bg-light" 
                           placeholder="Trả lời ${userName}..." 
                           onkeydown="if(event.keyCode==13){ sendComment(${postId}, ${parentId}); }">
                    <button class="btn btn-link p-0 position-absolute end-0 top-50 translate-middle-y me-2" 
                            onclick="sendComment(${postId}, ${parentId})">
                        <i class="bi bi-send-fill text-primary"></i>
                    </button>
                </div>
            </div>`;
        container.html(html);
    }

    container.removeClass('d-none');
    setTimeout(() => $(`#input-reply-${parentId}`).focus(), 100);
}
function deleteComment(commentId) {
    Swal.fire({
        title: 'Xóa bình luận?',
        text: 'Bình luận này và các câu trả lời sẽ bị xóa vĩnh viễn.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Xóa',
        cancelButtonText: 'Hủy',
        confirmButtonColor: '#dc3545'
    }).then((result) => {
        if (result.isConfirmed) {
            $.post('/Interactions/DeleteComment', { id: commentId }, function (res) {
                if (res.success) {
                    $(`#cmt-${commentId}`).fadeOut(300, function () { $(this).remove(); });
                } else {
                    Swal.fire('Lỗi', res.message, 'error');
                }
            });
        }
    });
}

$(document).ready(function () {
    const urlParams = new URLSearchParams(window.location.search);
    const hlPostId = urlParams.get('postId');
    const hlCommentId = urlParams.get('commentId');

    if (hlPostId) {
        const postElement = document.getElementById(`post-${hlPostId}`);
        if (postElement) {
            postElement.scrollIntoView({ behavior: 'smooth', block: 'center' });

            if (hlCommentId) {
                let commentBox = $(`#comment-box-${hlPostId}`);
                if (commentBox.hasClass('d-none')) {
                    toggleComment(hlPostId);
                }

                let attempts = 0;
                let interval = setInterval(() => {
                    attempts++;
                    let cmtElement = document.getElementById(`cmt-${hlCommentId}`);

                    if (cmtElement) {
                        clearInterval(interval);
                        doHighlight(cmtElement);
                    } else if (attempts >= 10) {
                        clearInterval(interval);
                    }
                }, 500);
            }
        }
    }
});

function doHighlight(element) {
    element.scrollIntoView({ behavior: 'smooth', block: 'center' });

    $(element).addClass('highlight-comment');

    setTimeout(() => {
        $(element).removeClass('highlight-comment');
    }, 3000);
}

function openMediaViewer(mediaJson, startIndex) {
    document.querySelectorAll('video').forEach(v => v.pause());

    if (typeof videojs !== 'undefined') {
        var players = videojs.getAllPlayers();
        players.forEach(function (player) { player.pause(); });
    }

    currentMediaList = mediaJson;
    currentMediaIndex = startIndex;

    const viewer = document.getElementById('media-viewer');
    viewer.style.display = 'block';
    document.body.style.overflow = 'hidden';

    renderMedia();
}

function closeMediaViewer() {
    const viewer = document.getElementById('media-viewer');
    viewer.style.display = 'none';
    document.body.style.overflow = '';

    if (currentPlayer) {
        currentPlayer.dispose();
        currentPlayer = null;
    }
    document.getElementById('mv-media-container').innerHTML = '';
}

function renderMedia() {
    const container = document.getElementById('mv-media-container');
    const media = currentMediaList[currentMediaIndex];
    const prevBtn = document.getElementById('mv-prev');
    const nextBtn = document.getElementById('mv-next');

    if (currentPlayer) {
        currentPlayer.dispose();
        currentPlayer = null;
    }
    container.innerHTML = '';

    if (media.type === 'Video') {
        const videoElement = document.createElement('video');
        videoElement.className = 'video-js vjs-default-skin vjs-big-play-centered';
        videoElement.style.width = '100%';
        videoElement.style.height = '100%';

        const source = document.createElement('source');
        source.src = media.url;
        videoElement.appendChild(source);
        container.appendChild(videoElement);

        currentPlayer = videojs(videoElement, {
            controls: true,
            autoplay: true,
            preload: 'auto',
            fluid: false,
            fill: true
        });
    } else {
        const img = document.createElement('img');
        img.src = media.url;
        container.appendChild(img);
    }

    if (currentMediaList.length > 1) {
        prevBtn.style.display = 'flex';
        nextBtn.style.display = 'flex';
    } else {
        prevBtn.style.display = 'none';
        nextBtn.style.display = 'none';
    }
}

function nextMedia(e) { e.stopPropagation(); currentMediaIndex = (currentMediaIndex + 1) % currentMediaList.length; renderMedia(); }
function prevMedia(e) { e.stopPropagation(); currentMediaIndex = (currentMediaIndex - 1 + currentMediaList.length) % currentMediaList.length; renderMedia(); }

document.addEventListener('keydown', function (event) {
    if (document.getElementById('media-viewer').style.display === 'block') {
        if (event.key === "Escape") closeMediaViewer();
        if (event.key === "ArrowRight") nextMedia(event);
        if (event.key === "ArrowLeft") prevMedia(event);
    }
});