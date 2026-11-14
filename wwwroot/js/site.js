// VocaLearn Core JavaScript Utilities

// Text-to-Speech (TTS) Pronunciation Helper
window.speakWord = function(text, lang = 'en-US') {
  if (!('speechSynthesis' in window)) {
    console.warn('Text-to-Speech is not supported by this browser.');
    return;
  }
  
  window.speechSynthesis.cancel(); // Stop any pending speech
  
  // Vocabulary entries may contain parts of speech, for example "sign (n, v)".
  // Read only the vocabulary word itself.
  const spokenText = String(text || '').replace(/\([^)]*\)/g, '').replace(/\s+/g, ' ').trim();
  if (!spokenText) return;

  const utterance = new SpeechSynthesisUtterance(spokenText);
  utterance.lang = lang;
  utterance.rate = 0.9; // Slightly slower for clear pronunciation learning
  utterance.pitch = 1.0;
  
  // Pick preferred natural English voice if available
  const voices = window.speechSynthesis.getVoices();
  const naturalVoice = voices.find(v => v.lang.startsWith('en') && (v.name.includes('Natural') || v.name.includes('Google') || v.name.includes('Premium')));
  if (naturalVoice) {
    utterance.voice = naturalVoice;
  }
  
  window.speechSynthesis.speak(utterance);
};

// Initialize mobile navigation
document.addEventListener('DOMContentLoaded', function() {
  const toggleBtn = document.getElementById('mobileMenuToggle');
  const sidebar = document.querySelector('.app-sidebar');
  
  if (toggleBtn && sidebar) {
    toggleBtn.addEventListener('click', function(e) {
      e.stopPropagation();
      sidebar.classList.toggle('show-mobile');
    });

    document.addEventListener('click', function(e) {
      if (!sidebar.contains(e.target) && !toggleBtn.contains(e.target)) {
        sidebar.classList.remove('show-mobile');
      }
    });
  }

  // Auto-hide alert messages after 5 seconds
  const alerts = document.querySelectorAll('.alert-vl');
  alerts.forEach(function(alert) {
    setTimeout(function() {
      alert.style.transition = 'opacity 0.5s ease';
      alert.style.opacity = '0';
      setTimeout(() => alert.remove(), 500);
    }, 5000);
  });
});
