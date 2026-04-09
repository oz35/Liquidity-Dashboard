import streamlit as st
import re
import pandas as pd
import plotly.graph_objects as go
from plotly.subplots import make_subplots
from fredapi import Fred
import yfinance as yf

# Set up the web page title
st.set_page_config(page_title="G3 Global Liquidity Dashboard", layout="centered")
st.title("G3 Global Liquidity vs. Assets")
st.write("Tracking the combined USD value of the Fed, ECB, and BOJ.")

# Create a box for you to paste your API key
api_key = st.text_input("Enter your FRED API Key:", type="password")

# Define the assets for the drop-down menu
assets = {
    "Bitcoin (BTC)": "BTC-USD",
    "S&P 500 (US500)": "^GSPC",
    "Bittensor (TAO)": "TAO-USD",
    "Astar (ASTR)": "ASTR-USD"
}

# Create the interactive drop-down menu
selected_asset_name = st.selectbox("Select an asset to compare:", list(assets.keys()))
selected_ticker = assets[selected_asset_name]

if api_key:
    # Basic validation: FRED API keys are 32-character alphanumeric strings
    api_key = api_key.strip()
    if not re.fullmatch(r'[a-z0-9]{32}', api_key.lower()):
        st.error("Invalid FRED API Key format. It should be a 32-character alphanumeric string.")
        st.stop()

    try:
        fred = Fred(api_key=api_key)
        
        with st.spinner("Fetching global central bank data & exchange rates..."):
            # 1. Pull US Federal Reserve Data
            walcl = fred.get_series('WALCL')       
            wtregen = fred.get_series('WTREGEN')   
            rrp = fred.get_series('RRPONTSYD')     
            
            # 2. Pull ECB and BOJ Balance Sheets
            ecb_assets = fred.get_series('ECBASSETSW') # ECB in Millions of Euros
            boj_assets = fred.get_series('JPNASSETS')  # BOJ in Billions of Yen
            
            # 3. Pull Live Exchange Rates to convert everything to USD
            eur_usd = fred.get_series('DEXUSEU') # 1 Euro = X USD
            usd_jpy = fred.get_series('DEXJPUS') # 1 USD = X Yen

            # Combine them into a single table
            df = pd.DataFrame({
                'WALCL': walcl, 'WTREGEN': wtregen, 'RRP': rrp,
                'ECB_EUR': ecb_assets, 'BOJ_JPY': boj_assets,
                'EUR_USD': eur_usd, 'USD_JPY': usd_jpy
            })
            
            # Forward-fill the data 
            df = df.ffill().dropna()

            # --- THE GLOBAL MATH ---
            
            # Calculate US Fed Net Liquidity (in Trillions)
            df['Fed_Trillions'] = (df['WALCL'] - df['WTREGEN'] - (df['RRP'] * 1000)) / 1000000
            
            # Calculate ECB Assets in USD (in Trillions)
            df['ECB_USD_Trillions'] = (df['ECB_EUR'] * df['EUR_USD']) / 1000000
            
            # Calculate BOJ Assets in USD (in Trillions)
            df['BOJ_USD_Trillions'] = df['BOJ_JPY'] / (df['USD_JPY'] * 1000)

            # Combine them for the G3 Global Index!
            df['G3_Global_Liquidity'] = df['Fed_Trillions'] + df['ECB_USD_Trillions'] + df['BOJ_USD_Trillions']

            # Filter to show just the last 5 years
        

            # Fetch Asset Data to match our dates
            start_date = df.index.min().strftime('%Y-%m-%d')
            end_date = df.index.max().strftime('%Y-%m-%d')
            asset_data = yf.download(selected_ticker, start=start_date, end=end_date)
            
            # Create a dual-axis chart
            fig = make_subplots(specs=[[{"secondary_y": True}]])

            # Add G3 Liquidity Line (Neon Pink)
            fig.add_trace(
                go.Scatter(x=df.index, y=df['G3_Global_Liquidity'], name="G3 Global Liquidity ($ Trillions)", line=dict(color='#FF00FF', width=2)),
                secondary_y=False,
            )

            # Add Asset Line (Blue)
            fig.add_trace(
                go.Scatter(x=asset_data.index, y=asset_data['Close'].squeeze(), name=selected_asset_name, line=dict(color='#00BFFF', width=2)),
                secondary_y=True,
            )

            # Format the chart
            fig.update_layout(
                title_text=f"G3 Global Liquidity vs. {selected_asset_name}",
                template="plotly_dark",
                hovermode="x unified"
            )
            fig.update_yaxes(title_text="Global Liquidity ($ Trillions)", secondary_y=False)
            fig.update_yaxes(title_text=f"{selected_asset_name} Price", secondary_y=True)

            # Display the chart
            st.plotly_chart(fig)
            
            st.success("Global G3 Chart updated successfully!")

    except Exception as e:
        st.error(f"Oops! Something went wrong: {e}. Double check your API key.")
