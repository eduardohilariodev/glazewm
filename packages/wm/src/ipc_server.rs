use std::sync::Arc;

use anyhow::Result;
use futures_util::{SinkExt, StreamExt};
use ipc_client::DEFAULT_IPC_ADDR;
use tokio::{
  net::TcpListener,
  sync::{
    mpsc::{self},
    Mutex,
  },
  task,
};
use tokio_tungstenite::accept_async;
use tracing::info;

use crate::{
  app_command::InvokeCommand, wm_event::WmEvent, wm_state::WmState,
};

#[derive(Debug)]
pub enum IpcMessage {
  Monitors,
  Windows,
  InvokeCommand,
  Subscribe,
}

pub struct IpcServer {
  pub message_rx: mpsc::UnboundedReceiver<IpcMessage>,
  pub wm_command_rx: mpsc::UnboundedReceiver<InvokeCommand>,
}

impl IpcServer {
  pub async fn start() -> Result<Self> {
    let (message_tx, message_rx) = mpsc::unbounded_channel::<IpcMessage>();

    let (wm_command_tx, wm_command_rx) =
      mpsc::unbounded_channel::<InvokeCommand>();

    let server = TcpListener::bind(DEFAULT_IPC_ADDR).await?;

    task::spawn(async move {
      while let Ok((stream, _)) = server.accept().await {
        let mut ws_stream = accept_async(stream).await.unwrap();
        info!("Received new IPC connection.");

        while let Some(msg) = ws_stream.next().await {
          let msg = msg.unwrap();
          if msg.is_text() || msg.is_binary() {
            ws_stream.send(msg).await.unwrap();
          }
        }
      }
    });

    Ok(Self {
      message_rx,
      wm_command_rx,
    })
  }

  pub async fn stop(&self) {
    todo!()
  }

  pub async fn process_message(
    &self,
    _message: IpcMessage,
    wm_state: Arc<Mutex<WmState>>,
  ) {
    // TODO: Spawn a task so that it doesn't block main thread execution.
  }

  pub async fn process_event(&mut self, event: WmEvent) {
    // TODO: Spawn a task so that it doesn't block main thread execution.
  }
}
