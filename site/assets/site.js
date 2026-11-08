const sampleWindow = document.querySelector('#demo-window');
const list = document.querySelector('#sample-list');
const note = document.querySelector('#preview-note');
const sampleTasks = ['整理下一步计划', '留一点时间喝杯水', '检查今天的小进展'];
let nextSample = 0;
document.querySelectorAll('[data-mode]').forEach(button => {
  button.addEventListener('click', () => {
    const compact = button.dataset.mode === 'compact';
    sampleWindow.classList.toggle('compact', compact);
    document.querySelectorAll('[data-mode]').forEach(tab => {
      const selected = tab === button;
      tab.classList.toggle('selected', selected);
      tab.setAttribute('aria-pressed', String(selected));
    });
  });
});
list.addEventListener('click', event => {
  const button = event.target.closest('.sample-check');
  if (!button) return;
  button.closest('li').remove();
  const task = sampleTasks[nextSample++ % sampleTasks.length];
  const row = document.createElement('li');
  const grip = document.createElement('span'); grip.className = 'handle'; grip.textContent = '⠿'; grip.setAttribute('aria-hidden', 'true');
  const check = document.createElement('button'); check.className = 'sample-check'; check.setAttribute('aria-label', `完成示例事项：${task}`);
  const text = document.createElement('div'); text.textContent = task;
  row.append(grip, check, text); list.append(row);
  note.textContent = '已完成一条示例事项，下一条自动补位。真实待办仅在桌面程序中保存。';
});
document.querySelector('#remind-demo').addEventListener('click', () => {
  const first = list.firstElementChild;
  first.classList.remove('pulse');
  void first.offsetWidth;
  first.classList.add('pulse');
  note.textContent = '对应事项柔和闪烁三次，不发出声音。这里演示网页中的视觉效果。';
});
