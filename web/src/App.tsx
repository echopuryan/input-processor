import "@mantine/core/styles.css";

import { createTheme, MantineProvider } from "@mantine/core";

import "./App.css";
import InputProcessor from "./features/processor/components/InputProcessor";

const theme = createTheme({});

function App() {
  return (
    <MantineProvider theme={theme} defaultColorScheme="dark">
      <InputProcessor />
    </MantineProvider>
  );
}

export default App;
