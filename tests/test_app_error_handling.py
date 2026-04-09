import unittest
from unittest.mock import MagicMock, patch
import sys
import os

class TestAppErrorHandling(unittest.TestCase):
    def test_fred_api_error_handling(self):
        # Mocking all required modules
        mock_st = MagicMock()
        mock_pd = MagicMock()
        mock_plotly = MagicMock()
        mock_go = MagicMock()
        mock_subplots = MagicMock()
        mock_fredapi = MagicMock()
        mock_yf = MagicMock()

        # Setup st.text_input to return a key so the if api_key: block is entered
        mock_st.text_input.return_value = "fake_key"
        # Setup st.selectbox to return an asset
        mock_st.selectbox.return_value = "Bitcoin (BTC)"

        # Setup fredapi.Fred mock so that its get_series method raises an Exception
        mock_fred_instance = mock_fredapi.Fred.return_value
        mock_fred_instance.get_series.side_effect = Exception("FRED error")

        # Mocking sys.modules
        with patch.dict(sys.modules, {
            'streamlit': mock_st,
            'pandas': mock_pd,
            'plotly': mock_plotly,
            'plotly.graph_objects': mock_go,
            'plotly.subplots': mock_subplots,
            'fredapi': mock_fredapi,
            'yfinance': mock_yf
        }):
            # Read and execute app.py
            with open('app.py', 'r') as f:
                code = f.read()
                # Use a fresh globals dict to avoid side effects
                exec(code, {'__name__': '__main__'})

        # Assert st.error is called with the expected message
        expected_error_msg = "Oops! Something went wrong: FRED error. Double check your API key."
        mock_st.error.assert_called_once_with(expected_error_msg)

if __name__ == '__main__':
    unittest.main()
