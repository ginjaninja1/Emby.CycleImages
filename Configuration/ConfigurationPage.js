define([
        "loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle",
        "emby-collapse"
    ],
    function(loading, dialogHelper, mainTabsManager) {

        const pluginId = "600FF041-1129-441F-82D9-D3943F22C7BE";

        function getTabs() {
            return [{
                    href: Dashboard.getConfigurationPageUrl('ConfigurationPage'),
                    name: 'Cycle Images Configuration'
                }
                /*,
                                {
                                    href: Dashboard.getConfigurationPageUrl('PluginTab2ConfigurationPage'),
                                    name: 'PluginTab 2'
                                },
                                {
                                    href: Dashboard.getConfigurationPageUrl('PluginTab3ConfigurationPage'),
                                    name: 'PluginTab 3'
                                }*/
            ];
        }
        function LoadConfig(view, config) {

            ApiClient.getPluginConfiguration(pluginId).then(function(config) {

                view.querySelector(".chkEnableCycleImages").checked = config.EnableCycleImages;
                view.querySelector("#cycleTagString", view).value = config.CycleTagString || "";
            });
        }

        return function(view) {
            view.addEventListener('viewshow', async() => {

                loading.show();

                mainTabsManager.setTabs(this, 0, getTabs);

                var config = await ApiClient.getPluginConfiguration(pluginId);
                LoadConfig(view, config);

                loading.hide();

                document.querySelector('.pageTitle').innerHTML = "Cycle Images" + '<a is="emby-linkbutton" class="raised raised-mini emby-button" target="_blank" href="https://emby.media/community/topic/116139-ginjaninja-tools-cycle-images-plugin-replace-collection-collage-image-based-on-latest-members/"><i class="md-icon button-icon button-icon-left secondaryText headerHelpButtonIcon">help</i><span class="headerHelpButtonText">Help</span></a>';

                var enableCycleImages = view.querySelector(".chkEnableCycleImages");
                enableCycleImages.addEventListener('change',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableCycleImages = enableCycleImages.checked;
                            ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                                Dashboard.processPluginConfigurationUpdateResult(r);
                            });
                        });
                    });

                var cycleTagBtn = view.querySelector("#btnSaveCycleTag");
                var cycleTagString = view.querySelector("#cycleTagString");
                cycleTagBtn.addEventListener('click',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.CycleTagString = cycleTagString.value;
                            ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                                Dashboard.processPluginConfigurationUpdateResult(r);
                            });
                        });

                    });
            });
        };
    });