/**
 * Email Receipt Functionality
 * Handles the loading state of the email button when sending receipts
 */
document.addEventListener('DOMContentLoaded', function() {
    // Find all email forms on the page
    const emailForms = document.querySelectorAll('.email-form');
    
    // Add submit event listener to each form
    emailForms.forEach(form => {
        form.addEventListener('submit', function(e) {
            // Get the button and spinner elements
            const button = this.querySelector('.email-button');
            const spinner = button.querySelector('.spinner-border');
            
            // Disable the button and show the spinner
            button.disabled = true;
            spinner.classList.remove('d-none');
            
            // The form will submit normally
            // The button will remain in loading state until the page reloads
        });
    });
});