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

  function setConnectionStatus(status, msg, ok) {
    status.textContent = msg;
    status.style.color = ok ? '#4caf50' : '#e53935';
  }

  async function testConnection(service, urlInput, keyInput, button, status) {
    button.disabled = true;
    setConnectionStatus(status, 'Testing...', true);

    try {
      const result = await window.ApiClient.ajax({
        type: 'POST',
        url: window.ApiClient.getUrl('plugins/ArrUnmonitor/TestConnection'),
        data: JSON.stringify({
          Service: service,
          Url: urlInput.value.trim(),
          ApiKey: keyInput.value.trim()
        }),
        contentType: 'application/json',
        dataType: 'json'
      });
      const success = result.Success === true || result.success === true;
      const message = result.Message || result.message || 'Connection test returned no result';
      setConnectionStatus(status, message, success);
    } catch {
      setConnectionStatus(status, 'Connection test failed', false);
    } finally {
      button.disabled = false;
    }
  }

  async function loadConfigPage(view) {
    try {
      const cfg = await window.ApiClient.getPluginConfiguration(GUID);
      field(view, 'chkEnabled').checked = cfg.Enabled !== false;
      field(view, 'chkDryRun').checked = cfg.DryRun === true;
      field(view, 'chkProcessMovies').checked = cfg.ProcessMovies !== false;
      field(view, 'chkProcessSeries').checked = cfg.ProcessSeries !== false;
      field(view, 'chkProcessSportarr').checked = cfg.ProcessSportarr !== false;
      field(view, 'chkProcessSeerr').checked = cfg.ProcessSeerr !== false;
      field(view, 'chkRequireProviderId').checked = cfg.RequireProviderId !== false;
      field(view, 'txtRadarrUrl').value = cfg.RadarrUrl || '';
      field(view, 'txtRadarrApiKey').value = cfg.RadarrApiKey || '';
      field(view, 'txtSonarrUrl').value = cfg.SonarrUrl || '';
      field(view, 'txtSonarrApiKey').value = cfg.SonarrApiKey || '';
      field(view, 'txtSportarrUrl').value = cfg.SportarrUrl || '';
      field(view, 'txtSportarrApiKey').value = cfg.SportarrApiKey || '';
      field(view, 'txtSeerrUrl').value = cfg.SeerrUrl || '';
      field(view, 'txtSeerrApiKey').value = cfg.SeerrApiKey || '';
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
    const processSeerrInput = field(view, 'chkProcessSeerr');
    const requireProviderIdInput = field(view, 'chkRequireProviderId');
    const radarrUrlInput = field(view, 'txtRadarrUrl');
    const radarrKeyInput = field(view, 'txtRadarrApiKey');
    const sonarrUrlInput = field(view, 'txtSonarrUrl');
    const sonarrKeyInput = field(view, 'txtSonarrApiKey');
    const sportarrUrlInput = field(view, 'txtSportarrUrl');
    const sportarrKeyInput = field(view, 'txtSportarrApiKey');
    const seerrUrlInput = field(view, 'txtSeerrUrl');
    const seerrKeyInput = field(view, 'txtSeerrApiKey');
    const connectionTests = [
      ['radarr', radarrUrlInput, radarrKeyInput, field(view, 'btnTestRadarr'), field(view, 'statusRadarr')],
      ['sonarr', sonarrUrlInput, sonarrKeyInput, field(view, 'btnTestSonarr'), field(view, 'statusSonarr')],
      ['sportarr', sportarrUrlInput, sportarrKeyInput, field(view, 'btnTestSportarr'), field(view, 'statusSportarr')],
      ['seerr', seerrUrlInput, seerrKeyInput, field(view, 'btnTestSeerr'), field(view, 'statusSeerr')]
    ];

    if (
      !form ||
      !saveBtn ||
      !enabledInput ||
      !dryRunInput ||
      !processMoviesInput ||
      !processSeriesInput ||
      !processSportarrInput ||
      !processSeerrInput ||
      !requireProviderIdInput ||
      !radarrUrlInput ||
      !radarrKeyInput ||
      !sonarrUrlInput ||
      !sonarrKeyInput ||
      !sportarrUrlInput ||
      !sportarrKeyInput ||
      !seerrUrlInput ||
      !seerrKeyInput ||
      connectionTests.some(test => test.some(value => !value))
    ) {
      return;
    }

    view.dataset.arrUnmonitorConfigBound = 'true';

    connectionTests.forEach(test => {
      const [service, urlInput, keyInput, button, status] = test;
      button.addEventListener('click', () => testConnection(service, urlInput, keyInput, button, status));
    });

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
        cfg.ProcessSeerr = processSeerrInput.checked;
        cfg.RequireProviderId = requireProviderIdInput.checked;
        cfg.RadarrUrl = radarrUrlInput.value.trim();
        cfg.RadarrApiKey = radarrKeyInput.value.trim();
        cfg.SonarrUrl = sonarrUrlInput.value.trim();
        cfg.SonarrApiKey = sonarrKeyInput.value.trim();
        cfg.SportarrUrl = sportarrUrlInput.value.trim();
        cfg.SportarrApiKey = sportarrKeyInput.value.trim();
        cfg.SeerrUrl = seerrUrlInput.value.trim();
        cfg.SeerrApiKey = seerrKeyInput.value.trim();
        await window.ApiClient.updatePluginConfiguration(GUID, cfg);

        const saved = await window.ApiClient.getPluginConfiguration(GUID);
        const savedOk =
          saved.Enabled === cfg.Enabled &&
          saved.DryRun === cfg.DryRun &&
          saved.ProcessMovies === cfg.ProcessMovies &&
          saved.ProcessSeries === cfg.ProcessSeries &&
          saved.ProcessSportarr === cfg.ProcessSportarr &&
          saved.ProcessSeerr === cfg.ProcessSeerr &&
          saved.RequireProviderId === cfg.RequireProviderId &&
          matchesSavedValue(saved.RadarrUrl, cfg.RadarrUrl) &&
          matchesSavedValue(saved.RadarrApiKey, cfg.RadarrApiKey) &&
          matchesSavedValue(saved.SonarrUrl, cfg.SonarrUrl) &&
          matchesSavedValue(saved.SonarrApiKey, cfg.SonarrApiKey) &&
          matchesSavedValue(saved.SportarrUrl, cfg.SportarrUrl) &&
          matchesSavedValue(saved.SportarrApiKey, cfg.SportarrApiKey) &&
          matchesSavedValue(saved.SeerrUrl, cfg.SeerrUrl) &&
          matchesSavedValue(saved.SeerrApiKey, cfg.SeerrApiKey);

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
