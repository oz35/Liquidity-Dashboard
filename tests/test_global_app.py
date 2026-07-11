import sys
import unittest.mock
from unittest.mock import MagicMock, ANY

def test_global_app_error_path():
    # 1. Create mocks for all required modules
    mock_st = MagicMock()
    # Need to mock the text_input to return an API key so it enters `if api_key:`
    mock_st.text_input.return_value = "dummy_api_key_123456789012345678"
    mock_st.selectbox.return_value = "Bitcoin (BTC)"

    mock_pd = MagicMock()
    mock_plotly = MagicMock()
    mock_go = MagicMock()
    mock_subplots = MagicMock()

    mock_fredapi = MagicMock()
    # Make Fred class instantiation raise an exception
    mock_fred_class = MagicMock(side_effect=Exception("API limit reached"))
    mock_fredapi.Fred = mock_fred_class

    mock_yf = MagicMock()

    # Create the dictionary of mocks to patch into sys.modules
    module_mocks = {
        'streamlit': mock_st,
        'pandas': mock_pd,
        'plotly': mock_plotly,
        'plotly.graph_objects': mock_go,
        'plotly.subplots': mock_subplots,
        'fredapi': mock_fredapi,
        'yfinance': mock_yf
    }

    with unittest.mock.patch.dict(sys.modules, module_mocks):
        # 2. Read the global_app.py script
        with open('global_app.py', 'r') as f:
            script_code = f.read()

        # 3. Execute the script within a controlled namespace
        namespace = {}
        exec(script_code, namespace)

        # 4. Verify the correct error path was taken
        # Since Fred(...) raises an exception, the except block should be hit
        # which calls st.error()
        mock_st.error.assert_called_once()

        # Verify the error message contains the exception details
        call_args = mock_st.error.call_args[0][0]
        assert "Oops! Something went wrong" in call_args
        assert "API limit reached" in call_args
