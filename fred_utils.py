import streamlit as st
from fredapi import Fred

@st.cache_resource
def get_fred_client(api_key):
    """
    Get a cached FRED API client.
    """
    return Fred(api_key=api_key)

@st.cache_data
def get_fred_series(api_key, series_id):
    """
    Fetch a data series from FRED with caching.
    """
    fred = get_fred_client(api_key)
    return fred.get_series(series_id)
