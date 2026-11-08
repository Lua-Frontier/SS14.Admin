document.addEventListener('DOMContentLoaded', function () {
    const x = document.getElementById('logTypeInput');
    const autocompleteOptions = document.getElementById('autocompleteOptions');
    let currentSelectedIndex = -1; // To keep track of the selected index

    x.addEventListener('input', function () {
        const currentInput = this.value.toLowerCase();
        const filteredValues = Object.keys(AdminLogType).filter(
            (key) => key.toLowerCase().includes(currentInput)
        );

        showAutocompleteOptions(filteredValues);
        highlightMatchingOption();
    });

    x.addEventListener('blur', function () {
        setTimeout(hideAutocompleteOptions, 200);
    });

    x.addEventListener('focus', function () {
        const currentInput = this.value.toLowerCase();
        if (currentInput.length > 0) {
            const filteredValues = Object.keys(AdminLogType).filter(
                (key) => key.toLowerCase().includes(currentInput)
            );
            showAutocompleteOptions(filteredValues);
        }
    });

    x.addEventListener('keydown', function (event) {
        if (event.key === 'Tab') {
            const suggestions = autocompleteOptions.querySelectorAll('div');
            currentSelectedIndex++;
            if (currentSelectedIndex >= 0 && currentSelectedIndex < suggestions.length) {
                handleSelectedOption(suggestions);
                event.preventDefault(); // Prevent tab from moving to the next input
            } else if (currentSelectedIndex >= suggestions.length) {
                // Reset to the top of the list
                currentSelectedIndex = 0;
                handleSelectedOption(suggestions);
                event.preventDefault(); // Prevent tab from moving to the next input
            }
        } else if (event.key === 'Enter') {
            const suggestions = autocompleteOptions.querySelectorAll('div');
            if (currentSelectedIndex !== -1 && currentSelectedIndex < suggestions.length) {
                x.value = suggestions[currentSelectedIndex].textContent;
                hideAutocompleteOptions();
                event.preventDefault(); // Prevent form submission
            } else if (x.value && suggestions.length > 0) {
                x.value = suggestions[0].textContent;
                hideAutocompleteOptions();
                event.preventDefault(); // Prevent form submission
            }
        }
    });

    document.addEventListener('click', function (event) {
        if (event.target !== x && !autocompleteOptions.contains(event.target)) {
            hideAutocompleteOptions();
        }
    });

    autocompleteOptions.addEventListener('click', function (event) {
        if (event.target.tagName === 'DIV') {
            x.value = event.target.textContent;
            hideAutocompleteOptions();
        }
    });

    function showAutocompleteOptions(values) {
        autocompleteOptions.innerHTML = '';

        values.slice(0, 5).forEach((value) => {
            const option = document.createElement('div');
            option.textContent = value;
            autocompleteOptions.appendChild(option);
        });

        autocompleteOptions.style.display = 'block';
    }

    function hideAutocompleteOptions() {
        autocompleteOptions.style.display = 'none';
        currentSelectedIndex = -1; // Reset the selected index when hiding options
        const selectedOption = autocompleteOptions.querySelector('.selected');
        if (selectedOption) {
            selectedOption.classList.remove('selected');
        }
    }

    function handleSelectedOption(suggestions) {
        suggestions.forEach((option, index) => {
            if (index === currentSelectedIndex) {
                option.classList.add('selected');
                x.value = option.textContent;
            } else {
                option.classList.remove('selected');
            }
        });
    }

    function highlightMatchingOption() {
        const inputText = x.value.toLowerCase();
        const suggestions = autocompleteOptions.querySelectorAll('div');

        suggestions.forEach((option) => {
            if (option.textContent.toLowerCase() === inputText) {
                option.classList.add('highlighted');
            } else {
                option.classList.remove('highlighted');
            }
        });
    }

    // Disable click event on autocomplete options
    autocompleteOptions.addEventListener('click', function (event) {
        event.preventDefault();
    });
});
