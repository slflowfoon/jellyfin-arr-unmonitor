(function () {
  'use strict';

  const GUID = 'd8936cb9-b278-4fcf-897b-4a7dec1d9879';

  function field(view, id) {
    return view.querySelector('#' + id);
  }

  function setConfigStatus(view, msg, ok) {
    const el = field(view, 'statusMsg');
    if (!el) return;
    el.textContent = msg;
    el.style.color = ok ? '#4caf50' : '#e53935';
  }

  async function loadConfigPage(view) {
    try {
      const cfg = await window.ApiClient.getPluginConfiguration(GUID);
      field(view, 'chkEnabled').checked = cfg.Enabled !== false;
      field(view, 'chkDryRun').checked = cfg.DryRun === true;
      field(view, 'chkProcessMovies').checked = cfg.ProcessMovies !== false;
      field(view, 'chkProcessSeries').checked = cfg.ProcessSeries !== false;
      field(view, 'chkProcessSportarr').checked = cfg.ProcessSportarr !== false;
      field(view, 'chkRequireProviderId').checked = cfg.RequireProviderId !== false;
      field(view, 'txtRadarrUrl').value = cfg.RadarrUrl || '';
      field(view, 'txtRadarrApiKey').value = cfg.RadarrApiKey || '';
      field(view, 'txtSonarrUrl').value = cfg.SonarrUrl || '';
      field(view, 'txtSonarrApiKey').value = cfg.SonarrApiKey || '';
      field(view, 'txtSportarrUrl').value = cfg.SportarrUrl || '';
      field(view, 'txtSportarrApiKey').value = cfg.SportarrApiKey || '';
      setConfigStatus(view, '', true);
    } catch {
      setConfigStatus(view, 'Could not load plugin settings', false);
    }
  }

  function matchesSavedValue(actual, expected) {
    return String(actual || '').trim() === String(expected || '').trim();
  }

  function bindConfigPage(view) {
    if (!view || view.dataset.arrUnmonitorConfigBound === 'true') return;

    const form = field(view, 'arrUnmonitorConfigForm');
    const saveBtn = field(view, 'btnSave');
    const enabledInput = field(view, 'chkEnabled');
    const dryRunInput = field(view, 'chkDryRun');
    const processMoviesInput = field(view, 'chkProcessMovies');
    const processSeriesInput = field(view, 'chkProcessSeries');
    const processSportarrInput = field(view, 'chkProcessSportarr');
    const requireProviderIdInput = field(view, 'chkRequireProviderId');
    const radarrUrlInput = field(view, 'txtRadarrUrl');
    const radarrKeyInput = field(view, 'txtRadarrApiKey');
    const sonarrUrlInput = field(view, 'txtSonarrUrl');
    const sonarrKeyInput = field(view, 'txtSonarrApiKey');
    const sportarrUrlInput = field(view, 'txtSportarrUrl');
    const sportarrKeyInput = field(view, 'txtSportarrApiKey');

    if (
      !form ||
      !saveBtn ||
      !enabledInput ||
      !dryRunInput ||
      !processMoviesInput ||
      !processSeriesInput ||
      !processSportarrInput ||
      !requireProviderIdInput ||
      !radarrUrlInput ||
      !radarrKeyInput ||
      !sonarrUrlInput ||
      !sonarrKeyInput ||
      !sportarrUrlInput ||
      !sportarrKeyInput
    ) {
      return;
    }

    view.dataset.arrUnmonitorConfigBound = 'true';

    form.addEventListener('submit', async evt => {
      evt.preventDefault();
      saveBtn.disabled = true;
      setConfigStatus(view, 'Saving...', true);

      try {
        const cfg = await window.ApiClient.getPluginConfiguration(GUID);
        cfg.Enabled = enabledInput.checked;
        cfg.DryRun = dryRunInput.checked;
        cfg.ProcessMovies = processMoviesInput.checked;
        cfg.ProcessSeries = processSeriesInput.checked;
        cfg.ProcessSportarr = processSportarrInput.checked;
        cfg.RequireProviderId = requireProviderIdInput.checked;
        cfg.RadarrUrl = radarrUrlInput.value.trim();
        cfg.RadarrApiKey = radarrKeyInput.value.trim();
        cfg.SonarrUrl = sonarrUrlInput.value.trim();
        cfg.SonarrApiKey = sonarrKeyInput.value.trim();
        cfg.SportarrUrl = sportarrUrlInput.value.trim();
        cfg.SportarrApiKey = sportarrKeyInput.value.trim();
        await window.ApiClient.updatePluginConfiguration(GUID, cfg);

        const saved = await window.ApiClient.getPluginConfiguration(GUID);
        const savedOk =
          saved.Enabled === cfg.Enabled &&
          saved.DryRun === cfg.DryRun &&
          saved.ProcessMovies === cfg.ProcessMovies &&
          saved.ProcessSeries === cfg.ProcessSeries &&
          saved.ProcessSportarr === cfg.ProcessSportarr &&
          saved.RequireProviderId === cfg.RequireProviderId &&
          matchesSavedValue(saved.RadarrUrl, cfg.RadarrUrl) &&
          matchesSavedValue(saved.RadarrApiKey, cfg.RadarrApiKey) &&
          matchesSavedValue(saved.SonarrUrl, cfg.SonarrUrl) &&
          matchesSavedValue(saved.SonarrApiKey, cfg.SonarrApiKey) &&
          matchesSavedValue(saved.SportarrUrl, cfg.SportarrUrl) &&
          matchesSavedValue(saved.SportarrApiKey, cfg.SportarrApiKey);

        if (!savedOk) {
          throw new Error('Jellyfin returned different plugin configuration after save');
        }

        setConfigStatus(view, 'Saved', true);
      } catch {
        setConfigStatus(view, 'Save failed or did not persist', false);
      } finally {
        saveBtn.disabled = false;
      }
    });

    loadConfigPage(view);
  }

  function initConfigPage() {
    bindConfigPage(document.getElementById('arrUnmonitorConfigPage'));
  }

  document.addEventListener('pageshow', initConfigPage);
  initConfigPage();
})();
