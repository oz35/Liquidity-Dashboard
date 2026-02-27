import streamlit as st
import pandas as pd
import plotly.graph_objects as go
from plotly.subplots import make_subplots
from fredapi import Fred
import yfinance as yf

# Set up the web page title
st.set_page_config(page_title="Global Liquidity Macro Dashboard", layout="centered")
st.title("Net Dollar Liquidity vs. Global Assets")
st.write("Tracking 'Shadow QE' and its impact on the markets.")

# Create a box for you to paste your API key securely
api_key = st.text_input("Enter your FRED API Key:", type="password")

# Define the assets for the drop-down menu
assets = {
    "Bitcoin (BTC)": "BTC-USD",
    "S&P 500 (US500)": "^GSPC",
    "Gold (Futures)": "GC=F",
    "Bittensor (TAO)": "TAO-USD",
    "Astar (ASTR)": "ASTR-USD"
}

# Create the interactive drop-down menu
selected_asset_name = st.selectbox("Select an asset to compare against Liquidity:", list(assets.keys()))
selected_ticker = assets[selected_asset_name]

if api_key:
    try:
        # Connect to the Fed
        fred = Fred(api_key=api_key)
        
        with st.spinner(f"Fetching data for Liquidity and {selected_asset_name}..."):
            # Pull the three specific datasets
            walcl = fred.get_series('WALCL')       
            wtregen = fred.get_series('WTREGEN')   
            rrp = fred.get_series('RRPONTSYD')     

            # Combine them into a single table
            df = pd.DataFrame({'WALCL': walcl, 'WTREGEN': wtregen, 'RRP': rrp})
            df = df.ffill().dropna()

            # Calculate Net Liquidity
            df['RRP_Millions'] = df['RRP'] * 1000
            df['Net_Liquidity_Millions'] = df['WALCL'] - df['WTREGEN'] - df['RRP_Millions']
            df['Net_Liquidity_Trillions'] = df['Net_Liquidity_Millions'] / 1000000

            # Filter to show just the last 5 years
            df = df.last('5Y')

            # Fetch Asset Data to match our dates
            start_date = df.index.min().strftime('%Y-%m-%d')
            end_date = df.index.max().strftime('%Y-%m-%d')
            
            # Pull the data for whatever the user selected in the drop-down
            asset_data = yf.download(selected_ticker, start=start_date, end=end_date)
            
            # Create a dual-axis chart
            fig = make_subplots(specs=[[{"secondary_y": True}]])

            # Add Liquidity Line (Neon Green)
            fig.add_trace(
                go.Scatter(x=df.index, y=df['Net_Liquidity_Trillions'], name="Net Liquidity ($ Trillions)", line=dict(color='#00FFAA', width=2)),
                secondary_y=False,
            )

            # Add Asset Line (Blue)
            fig.add_trace(
                go.Scatter(x=asset_data.index, y=asset_data['Close'].squeeze(), name=selected_asset_name, line=dict(color='#00BFFF', width=2)),
                secondary_y=True,
            )

            # Format the chart
            fig.update_layout(
                title_text=f"U.S. Net Dollar Liquidity vs. {selected_asset_name}",
                template="plotly_dark",
                hovermode="x unified"
            )
            fig.update_yaxes(title_text="Liquidity ($ Trillions)", secondary_y=False)
            fig.update_yaxes(title_text=f"{selected_asset_name} Price", secondary_y=True)

            # Display the chart on the app
            st.plotly_chart(fig)
            
            st.success("Chart updated successfully!")

    except Exception as e:
        st.error(f"Oops! Something went wrong: {e}. Double check your API key.")
