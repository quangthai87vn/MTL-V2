// windowSizeService.js

window.windowSizeService = {
    getWindowSize: function () {
        return {
            width: window.innerWidth,
            height: window.innerHeight
        };
    }
};

window.showMenu = function (toggleId, navId) {
    
        const toggle = document.getElementById(toggleId),
            nav = document.getElementById(navId)

        toggle.addEventListener('click', () => {
            // Add show-menu class to nav menu
            nav.classList.toggle('show-menu')

            // Add show-icon to show and hide the menu icon
            toggle.classList.toggle('show-icon')
        })
    
};

window.closeDrawer = function () {
    document.querySelector('#HomeMudDrawer').close(); // Thay '#yourDrawerId' bằng id của MudDrawer
}