import unittest
from unittest.mock import MagicMock, patch
import sys

class TestAppErrorHandling(unittest.TestCase):
    def test_api_failure_shows_error_message(self):
        """
        Test that an API failure in fredapi.Fred.get_series correctly triggers
        the error handling in app.py and displays a message via st.error.
        """
        # Create mocks for all dependencies to avoid ImportError and actual API calls
        mock_st = MagicMock()
        mock_fredapi = MagicMock()

        # Configure Fred instance to raise an exception when get_series is called
        mock_fred_instance = mock_fredapi.Fred.return_value
        mock_fred_instance.get_series.side_effect = Exception("API Error")

        # Mock streamlit return values to simulate user input
        mock_st.text_input.return_value = "fake_api_key"
        mock_st.selectbox.return_value = "Bitcoin (BTC)"

        # Patch dependencies that are actually imported in app.py
        # We use patch.dict on sys.modules to satisfy 'import' statements
        # and provide our mocks.
        with patch.dict(sys.modules, {
            "streamlit": mock_st,
            "fredapi": mock_fredapi,
            "pandas": MagicMock(),
            "plotly": MagicMock(),
            "plotly.graph_objects": MagicMock(),
            "plotly.subplots": MagicMock(),
            "yfinance": MagicMock()
        }):
            # Execute app.py
            with open("app.py", "r") as f:
                code = f.read()
            exec(code, {"__name__": "__main__"})

            # Verify st.error was called with the expected error message
            mock_st.error.assert_called_once()
            error_arg = mock_st.error.call_args[0][0]
            self.assertIn("Oops! Something went wrong: API Error. Double check your API key.", error_arg)

if __name__ == "__main__":
    unittest.main()
