// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
(function () {
  'use strict';

  // Ensure toast container exists
  function ensureToastContainer() {
    let container = document.getElementById('toast-stack');
    if (!container) {
      container = document.createElement('div');
      container.id = 'toast-stack';
      container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
      container.style.zIndex = '12000';
      document.body.appendChild(container);
    }
    return container;
  }

  // Bootstrap Toast helper (single, themed, elegant)
  window.toasts = {
    show: function (title, body, type) {
      var container = ensureToastContainer();
      var id = 't' + Date.now();
      var wrapper = document.createElement('div');
      wrapper.className = 'toast align-items-center border-0 shadow glass ' + (type || 'info');
      wrapper.setAttribute('role', 'status');
      wrapper.setAttribute('aria-live', 'polite');
      wrapper.setAttribute('aria-atomic', 'true');
      wrapper.id = id;
      wrapper.innerHTML = [
        '<div class="toast-header">',
        '  <strong class="me-auto">' + (title || 'Notice') + '</strong>',
        '  <small class="text-muted">now</small>',
        '  <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>',
        '</div>',
        ' <div class="toast-body">' + (body || '') + '</div>'
      ].join('');
      // Remove any existing toasts for elegance/discretion
      container.querySelectorAll('.toast').forEach(t => t.remove());
      container.appendChild(wrapper);
      var t = new bootstrap.Toast(wrapper, { delay: 4000 });
      t.show();
      wrapper.addEventListener('hidden.bs.toast', function () { wrapper.remove(); });
      return t;
    }
  };

  // Back to top button
  var backBtn = document.getElementById('backToTop');
  if (backBtn) {
    window.addEventListener('scroll', function () {
      var y = window.scrollY || document.documentElement.scrollTop;
      backBtn.style.display = y > 300 ? 'inline-flex' : 'none';
    });
    backBtn.addEventListener('click', function () { window.scrollTo({ top: 0, behavior: 'smooth' }); });
  }

  // Progress bar for navigation and async forms
  var progress = document.getElementById('progressbar');
  function setProgress(p) {
    if (!progress) return;
    var bar = progress.querySelector('.progress-bar');
    bar.style.width = Math.max(0, Math.min(100, p)) + '%';
    progress.style.opacity = p > 0 && p < 100 ? '1' : (p === 100 ? '0' : '0');
  }
  function startProgress() {
    setProgress(10);
    var i = 10;
    var iv = setInterval(function () {
      i += (100 - i) * 0.05;
      setProgress(i);
      if (i > 95) { clearInterval(iv); }
    }, 150);
    return function () {
      clearInterval(iv);
      setProgress(100);
      setTimeout(function () { setProgress(0); }, 300);
    };
  }

  // Link clicks (internal) -> progress
  document.addEventListener('click', function (e) {
    var a = e.target.closest('a');
    if (!a) return;
    var url = a.getAttribute('href');
    if (!url || url.startsWith('#') || a.target === '_blank' || url.startsWith('http')) return;
    var stop = startProgress();
    window.addEventListener('pageshow', function () { stop(); }, { once: true });
  });

  // Forms -> progress + disable button
  document.addEventListener('submit', function (e) {
    var form = e.target;
    var btn = form.querySelector('button[type="submit"]');
    var stop = startProgress();
    if (btn) {
      btn.disabled = true;
      btn.dataset._old = btn.innerHTML;
      btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Working...';
    }
    window.addEventListener('pageshow', function () {
      if (btn) { btn.disabled = false; btn.innerHTML = btn.dataset._old; }
      stop();
    }, { once: true });
  }, true);

  // IntersectionObserver reveal
  var reveal = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window && reveal.length) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) { if (entry.isIntersecting) { entry.target.classList.add('visible'); io.unobserve(entry.target); } });
    }, { rootMargin: '0px 0px -10% 0px', threshold: 0.1 });
    reveal.forEach(function (el) { io.observe(el); });
  } else {
    // Fallback
    reveal.forEach(function (el) { el.classList.add('visible'); });
  }

  // jQuery Validate -> toast on error summary
  if (window.jQuery) {
    (function ($) {
      $(function () {
        $(document).on('invalid-form.validate', function (event, validator) {
          var cnt = validator.numberOfInvalids();
          if (cnt > 0) { window.toasts.show('Please fix form', cnt + ' field' + (cnt > 1 ? 's' : '') + ' need attention.', 'error'); }
        });
      });
    })(window.jQuery);
  }

  // TempData-driven toasts via data attributes
  document.querySelectorAll('[data-toast]')
    .forEach(function (n) { window.toasts.show(n.getAttribute('data-title'), n.getAttribute('data-toast'), n.getAttribute('data-type')); });

  // Razor TempData toast (from body attributes)
  document.addEventListener('DOMContentLoaded', function () {
    var body = document.body;
    var message = body.getAttribute('data-toast-message');
    var type = body.getAttribute('data-toast-type');
    if (message && message.trim().length > 0) {
      window.toasts.show(type.charAt(0).toUpperCase() + type.slice(1), message, type);
    }
  });
  //window.toasts.show("Success", "Profile updated successfully!", "success");
  //// Or for other types:
  //window.toasts.show("Error", "Something went wrong.", "error");
  //window.toasts.show("Info", "Welcome to the site!", "info");
  //window.toasts.show("Warning", "Check your input.", "warning");
})();

// Example starter JavaScript for disabling form submissions if there are invalid fields
if (window.jQuery) {
  $(function () {
    $("form.needs-validation").on("submit", function (event) {
      var form = $(this);
      if (form[0].checkValidity() === false) {
        event.preventDefault();
        event.stopPropagation();
      }
      form.addClass('was-validated');
      form.find('.form-control').each(function () {
        if (!this.checkValidity()) {
          $(this).addClass('is-invalid');
        } else {
          $(this).removeClass('is-invalid');
        }
      });
    });
  });
}

// Star-rating click handler
if (window.jQuery) {
  $(function () {
    $('.star-rating .star').on('click', function () {
      var $star = $(this);
      var value = $star.data('value');
      var $container = $star.closest('.star-rating');
      var projectId = $container.data('project-id');
      var blogId = $container.data('blog-id');

      // Highlight selected stars
      $container.find('.star').each(function () {
        $(this).toggleClass('selected', $(this).data('value') <= value);
      });

      // Determine endpoint and payload consistently
      var isProject = typeof projectId !== 'undefined' && projectId !== false && projectId !== null;
      var endpoint = isProject ? '/api/rate' : '/api/blograte';
      var payload = isProject
        ? { projectId: projectId, stars: value }
        : { blogId: blogId, stars: value };

      // Send JSON payload matching server expectations
      $.ajax({
        url: endpoint,
        method: 'POST',
        contentType: 'application/json; charset=utf-8',
        data: JSON.stringify(payload),
        success: function () {
          window.toasts.show('Success', 'Thank you for your rating!', 'success');
        },
        error: function () {
          window.toasts.show('Error', 'Could not submit rating.', 'error');
        }
      });
    });
  });
}
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  // Do something async
  fetch('/some-api')
    .then(response => response.json())
    .then(data => sendResponse(data))
    .catch(err => sendResponse({ error: err.message }));

  return true; // Required for async response
});