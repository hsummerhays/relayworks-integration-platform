// Minimal vanilla interactivity for RelayWorks PHP Portal
document.addEventListener('DOMContentLoaded', () => {
    // Add auto-dismiss for alerts if needed or smooth interactions
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        alert.addEventListener('click', () => {
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 300);
        });
    });
});
