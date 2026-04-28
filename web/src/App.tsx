import '@mantine/core/styles.css';

import { createTheme, MantineProvider, Input, Button, Progress  } from '@mantine/core';

import './App.css'

const theme = createTheme({
});

function App() {
  return (
    <MantineProvider theme={theme}  defaultColorScheme="dark">
      <h1>Enter Your Input</h1>
      <Input placeholder="Input component" />
      <Progress value={50} />
      <section>
        <Button variant="outline">Process</Button>
        <Button variant="outline" color="red">Cancel</Button>
      </section>
      <div>Response Shown here</div>
    </MantineProvider>
  )
}

export default App
